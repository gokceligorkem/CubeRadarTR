"""Read-only AION2 motion experiment. Crypto secrets remain only in this helper's RAM."""
import argparse
import collections
import ctypes
import json
import os
import re
import struct
import time
from pathlib import Path
from read_only_locator import ProcessReader,locate_rc4_vtable,locate_states_via_owner_arena,locate_states,select_key,validate_rc4_state
from motion_wire import Rc4,FrameStream,find_alignment,parse_motion,clock_ok,recover_cursor
from passive_pcap import Pcap,connection_for
from target_wire import TargetSelectionState
from pose_observation import PoseObservation

VERSION='0.8.5'

class LocalPipeWriter:
    def __init__(self,name):
        if not re.fullmatch(r'CubeRadarMotion-[0-9a-f]{32}',name):raise ValueError('Geçersiz yerel kanal adı.')
        from ctypes import wintypes as w
        self.api=ctypes.WinDLL('kernel32',use_last_error=True)
        self.api.CreateFileW.argtypes=[w.LPCWSTR,w.DWORD,w.DWORD,ctypes.c_void_p,w.DWORD,w.DWORD,w.HANDLE]
        self.api.CreateFileW.restype=w.HANDLE
        self.api.WriteFile.argtypes=[w.HANDLE,ctypes.c_void_p,w.DWORD,ctypes.POINTER(w.DWORD),ctypes.c_void_p]
        self.api.WriteFile.restype=w.BOOL;self.api.CloseHandle.argtypes=[w.HANDLE]
        self.handle=self.api.CreateFileW('\\\\.\\pipe\\'+name,0x40000000,0,None,3,0,None)
        if self.handle==ctypes.c_void_p(-1).value:raise OSError('Yerel radar kanalına bağlanılamadı: '+str(ctypes.get_last_error()))
    def write(self,text):
        from ctypes import wintypes as w
        data=text.encode('utf-8');written=w.DWORD()
        if not self.api.WriteFile(self.handle,data,len(data),ctypes.byref(written),None) or written.value!=len(data):raise BrokenPipeError('Radar kapandı.')
    def flush(self):pass
    def close(self):
        if self.handle:self.api.CloseHandle(self.handle);self.handle=None

class LimitedReader(ProcessReader):
    def __init__(self,pid):
        super().__init__(pid);self.deadline=time.monotonic()+20;self.bytes_read=0;self.progress=None;self.last_progress=0
    def read(self,address,size):
        if time.monotonic()>self.deadline or self.bytes_read+size>3*1024**3:raise TimeoutError('Salt okunur arama sınırına ulaşıldı.')
        if self.progress is not None and time.monotonic()-self.last_progress>0.5:
            self.last_progress=time.monotonic();self.progress()
        self.bytes_read+=size;return super().read(address,size)

def snapshot(reader,addresses,vtable):
    results=[]
    for address in addresses:
        values=[]
        for _ in range(2):
            raw=reader.read(address,0x124)
            if raw is None:break
            i,j=struct.unpack_from('<II',raw,0x1c);s=raw[0x24:0x124]
            if i>255 or j>255 or sorted(s)!=list(range(256)):break
            values.append((s,i,j))
        if len(values)==2 and values[0]==values[1]:results.append(Rc4.snapshot(*values[0]))
    return results

def run(args,send):
    connection=connection_for(args.pid)
    if connection is None:raise ValueError('Etkin AION2 dünya bağlantısı yok.')
    capture=Pcap(connection)
    reader=LimitedReader(args.pid)
    reader.progress=lambda:send(dict(kind='status',text='Ağ şifreleme durumu aranıyor'))
    try:
        if reader.main_module()['name'].lower()!='aion2.exe':raise ValueError('Hedef AION2.exe değil.')
        send(dict(kind='status',text='Ağ şifreleme durumu aranıyor'))
        vtable,_=locate_rc4_vtable(reader,reader.main_module(),4*1024**2)
        inactive=set();pair_checks=0
        def active_pair(items,key):
            nonlocal pair_checks
            group=items[0]['key_sha256']
            if group in inactive:return False
            pair_checks+=1
            addresses=[int(item['address'],16) for item in items]
            before=[reader.read(address+0x1c,264) for address in addresses]
            time.sleep(0.15)
            after=[reader.read(address+0x1c,264) for address in addresses]
            active=any(a is not None and b is not None and a!=b for a,b in zip(before,after))
            if not active:inactive.add(group)
            return active
        hits,keys,_,_=locate_states_via_owner_arena(reader,vtable,None,None)
        selected=select_key(hits,keys)
        if selected is None or not active_pair([h for h in hits if h['key_sha256']==selected[0]],selected[1]):
            hits,keys,_=locate_states(reader,vtable,4*1024**2,32*1024**2,None,accept_pair=active_pair)
        selected=select_key(hits,keys)
        if selected is None:raise ValueError('İki eşleşen ağ şifreleme durumu bulunamadı.')
        session_key=selected[1]
        addresses=[int(item['address'],16) for item in hits if item['key_sha256']==selected[0]][:2]
        keys.clear();hits.clear()
        reader.progress=None
        reader.deadline=time.monotonic()+args.seconds+5
        send(dict(kind='status',text='Etkin şifre durumu bulundu; zaman kayıtları eşleniyor',inactivePairs=len(inactive),pairChecks=pair_checks))
        frames=collections.deque(maxlen=512);cipher=None;clocks=0;motions=0;gaps=0;last_clock=0
        last_align=0;last_status=0;start=time.monotonic();ended=False;snapshots=0;frame_count=0;clock_op=None;state_changes=0;previous_state=None
        clock_offset=0;correction=None;recovery_tried=False;motion_shapes=collections.Counter()
        target=TargetSelectionState();pose=PoseObservation()
        def reset():
            nonlocal cipher,clocks,gaps,clock_op
            cipher=None;clocks=0;clock_op=None;frames.clear();gaps+=1
            target.reset();send(dict(kind='target',**target.snapshot()))
            pose.reset()
        def decoded(t,plain):
            nonlocal clocks,motions,last_clock,cipher,clock_op
            if clock_ok(plain,t,clock_offset,clock_op):
                if clock_op is None:clock_op=plain[:2]
                if plain[:2]==clock_op:clocks+=1;last_clock=t
            elif clock_op is not None and plain[:2]==clock_op:
                reset();return
            if clocks>=3:
                selection=target.observe(plain,t)
                if selection is not None:send(dict(kind='target',**selection))
                if len(plain)>=2 and plain[1]==0x37:
                    shape=plain[:2].hex().upper()+':'+str(len(plain))
                    if shape in motion_shapes or len(motion_shapes)<24:motion_shapes[shape]+=1
                if (motion:=parse_motion(plain,t,clock_offset)):
                    motions+=1;send(dict(kind='motion',**pose.motion(motion)))
                elif len(plain)>=2 and plain[1]==0x37:
                    # An unknown movement form cannot leave a previous stop valid.
                    pose.reset()
        def on_frame(t,body):
            nonlocal frame_count
            frame_count+=1
            frames.append((t,body))
            if cipher is not None:decoded(t,cipher.crypt(body))
        stream=FrameStream(on_frame,reset)
        while time.monotonic()-start<args.seconds:
            packet=capture.next()
            if packet is not None:
                t,seq,flags,payload=packet
                if flags&5:ended=True;break
                stream.push(seq,payload,t)
            stream.expire(int(time.time()*1000))
            now=time.monotonic()
            if cipher is None and now-last_align>0.4 and len(frames)>=3:
                last_align=now
                current=snapshot(reader,addresses,vtable);snapshots=len(current)
                if current:
                    state_signature=tuple((tuple(r.s),r.i,r.j) for r in current)
                    if previous_state is not None and previous_state!=state_signature:state_changes+=1
                    previous_state=state_signature
                if correction is None and not recovery_tried and now-start>2 and current:
                    recovery_tried=True
                    recovered=recover_cursor(session_key,current[0])
                    if recovered is not None:correction=((recovered.i-current[0].i)&255,(recovered.j-current[0].j)&255)
                if correction is not None:
                    current+=[Rc4.snapshot(r.s,(r.i+correction[0])&255,(r.j+correction[1])&255) for r in current]
                for state in current:
                    saved=list(frames);aligned=find_alignment(state,saved)
                    if aligned is not None:
                        index=aligned.index;cipher=aligned.state;clock_offset=aligned.offset_ms;clock_op=aligned.opcode;clocks=0
                        for t,body in saved[index:]:
                            if cipher is None:break
                            decoded(t,cipher.crypt(body))
                        break
            if now-last_status>0.5:
                last_status=now
                send(dict(kind='health',streamReady=cipher is not None and clocks>=3,clocks=clocks,motions=motions,gaps=gaps,lastClockMs=last_clock,
                          frames=frame_count,snapshots=snapshots,stateChanges=state_changes,cursorRecovered=correction is not None,
                          clockOffsetMs=clock_offset,clockOpcode=clock_op.hex().upper() if clock_op else None,
                          helperVersion=VERSION,outOfOrder=stream.out_of_order,tcpGaps=stream.tcp_gaps,
                          pendingBytes=stream.pending_bytes,pendingSegments=len(stream.pending),motionShapes=dict(motion_shapes),
                          selectionProtocol=1,target=target.snapshot(),poseProtocol=1,pose=pose.heartbeat(last_clock)))
                if connection_for(args.pid)!=connection:ended=True;break
        send(dict(kind='status',text='Oyun bağlantısı değişti; okuyucuyu yeniden başlat.' if ended else 'Ölçüm tamamlandı'))
    finally:capture.close();reader.close()

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--version',action='version',version=VERSION)
    parser.add_argument('--pid',required=True,type=int)
    parser.add_argument('--seconds',type=int,default=90)
    parser.add_argument('--report',type=Path)
    parser.add_argument('--pipe')
    parser.add_argument('--status',type=Path)
    args=parser.parse_args();events=[];pipe=None
    latest={}
    if args.pipe:
        # Parent restricts the pipe to its Windows account. Only numeric motion/selection state crosses it.
        pipe=LocalPipeWriter(args.pipe)
    def send(value):
        if value.get('kind')=='motion':latest['motion']=value
        elif value.get('kind')=='health':latest['health']=value
        elif value.get('kind')=='target':latest['target']=value
        else:latest['status']=value
        if args.status is not None and value.get('kind')!='motion':
            temp=args.status.with_suffix('.tmp')
            temp.write_text(json.dumps(latest,ensure_ascii=False),encoding='utf-8');temp.replace(args.status)
        if pipe is not None:pipe.write(json.dumps(value,separators=(',',':'))+'\n');pipe.flush()
        if args.report is not None and len(events)<2000:events.append(value)
    try:run(args,send)
    except BrokenPipeError:pass
    except (Exception,SystemExit) as error:
        try:send(dict(kind='error',text=str(error) if isinstance(error,(OSError,ValueError,TimeoutError)) or 'Win32 error' in str(error) else 'Hareket okuyucu durdu.'))
        except BrokenPipeError:pass
    finally:
        if args.report is not None:args.report.write_text(json.dumps(dict(events=events,keysWritten=False,keysExported=False),ensure_ascii=False,indent=2),encoding='utf-8')
        if pipe is not None:
            try:pipe.close()
            except BrokenPipeError:pass
