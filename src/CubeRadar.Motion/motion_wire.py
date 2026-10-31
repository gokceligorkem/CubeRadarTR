"""Bounded passive C2S decoding. No packet contents, keys or states are logged."""
import math
import struct
from typing import NamedTuple

class Rc4:
    def __init__(self,key):
        self.s=list(range(256));self.i=self.j=0;j=0
        for i in range(256):
            j=(j+self.s[i]+key[i%len(key)])&255
            self.s[i],self.s[j]=self.s[j],self.s[i]
    @classmethod
    def snapshot(cls,s,i,j):
        if len(s)!=256 or sorted(s)!=list(range(256)) or not 0<=i<=255 or not 0<=j<=255:
            raise ValueError('Invalid RC4 state')
        r=cls.__new__(cls);r.s=list(s);r.i=i;r.j=j;return r
    def clone(self):return self.snapshot(self.s,self.i,self.j)
    def step(self):
        self.i=(self.i+1)&255;self.j=(self.j+self.s[self.i])&255
        self.s[self.i],self.s[self.j]=self.s[self.j],self.s[self.i]
        return self.s[(self.s[self.i]+self.s[self.j])&255]
    def back(self):
        value=self.s[(self.s[self.i]+self.s[self.j])&255]
        self.s[self.i],self.s[self.j]=self.s[self.j],self.s[self.i]
        self.j=(self.j-self.s[self.i])&255;self.i=(self.i-1)&255
        return value
    def shift(self,n):
        for _ in range(abs(n)):self.step() if n>=0 else self.back()
        return self
    def crypt(self,data):return bytes(b^self.step() for b in data)

def recover_cursor(key,memory,max_bytes=16*1024*1024):
    """Verify a runtime index convention against the key and the complete permutation."""
    r=Rc4(key);target=memory.s;prefix=target[:8]
    indices={memory.i,(memory.i-1)&255,(memory.i+1)&255}
    for _ in range(max_bytes+1):
        if r.i in indices and r.s[:8]==prefix and r.s==target:return r
        r.step()
    return None

def clock_ok(body,t,offset_ms=0,opcode=None):
    return len(body)==10 and body[1]==0x36 and (opcode is None or body[:2]==opcode) and abs(struct.unpack_from('<Q',body,2)[0]-offset_ms-t)<5000

class ClockAlignment(NamedTuple):
    index:int
    state:Rc4
    offset_ms:int
    opcode:bytes

def find_alignment(snapshot,frames,back=16384,forward=8192):
    """Align a current RAM state; require three same-opcode clocks with consistent timing."""
    reverse=snapshot.clone();past=bytes(reverse.back() for _ in range(back))[::-1]
    future=snapshot.clone();stream=past+bytes(future.step() for _ in range(forward))
    def verify(index,at):
        state=snapshot.clone().shift(at-back);trial=state.clone();clocks=0;opcode=None;offset=None
        for stamp,encrypted in frames[index:]:
            decoded=trial.crypt(encrypted)
            if opcode is None:
                if len(decoded)!=10 or decoded[1]!=0x36:return None
                opcode=decoded[:2];offset=struct.unpack_from('<Q',decoded,2)[0]-stamp
            if decoded[:2]==opcode:
                if not clock_ok(decoded,stamp,offset,opcode):return None
                clocks+=1
                if clocks>=3:return ClockAlignment(index,state,offset,opcode)
        return None
    for index in range(len(frames)-1,max(-1,len(frames)-24),-1):
        t,body=frames[index]
        if len(body)!=10:continue
        # Timestamp's high four bytes are stable for weeks. No opcode-shift assumption.
        high=struct.pack('<Q',t)[4:8]
        needle=bytes(b^k for b,k in zip(body[6:10],high));found=stream.find(needle)
        while found>=6:
            at=found-6
            if at+10>len(stream):break
            if (aligned:=verify(index,at)) is not None:return aligned
            found=stream.find(needle,found+1)
        # Fallback for a constant client clock offset. Full clock sequence validates it.
        for first in range(9):
            needle=bytes([body[0]^first,body[1]^0x36]);at=stream.find(needle)
            while at>=0 and at+10<=len(stream):
                if (aligned:=verify(index,at)) is not None:return aligned
                at=stream.find(needle,at+1)
    return None

def parse_motion(body,capture_ms,clock_offset_ms=0):
    op=body[:2];move=None;stopped=False
    if op in (b'\x00\x37',b'\x01\x37'):
        if len(body) not in (29,30) or body[2] not in (0,2):return None
        if (body[2]==0)!=(len(body)==29):return None
        x,y,z=struct.unpack_from('<fff',body,3)
        move=struct.unpack_from('<H',body,15)[0]*360/65536
        face=struct.unpack_from('<f',body,17)[0];stopped=body[2]==0
    elif op==b'\x18\x37':
        if len(body)!=28:return None
        x,y,z,face=struct.unpack_from('<ffff',body,4)
    elif op==b'\x03\x37':
        if len(body)!=40 or body[2]!=2:return None
        x,y,z,face=struct.unpack_from('<ffff',body,3)
    elif op in (b'\x0a\x37',b'\x0b\x37'):
        # Flight has velocity before heading; continuation has no flags byte.
        if op==b'\x0a\x37':
            if len(body) not in (40,41):return None
            if (len(body)==40 and body[2]!=2) or (len(body)==41 and (body[2]!=3 or body[31]!=3)):return None
            offset=3
        else:
            if len(body)!=39:return None
            offset=2
        if body[-9] not in (0,1):return None
        x,y,z,vx,vy,vz,face=struct.unpack_from('<fffffff',body,offset)
        if not all(math.isfinite(v) for v in (vx,vy,vz)):return None
        move=math.degrees(math.atan2(vy,vx)) if math.hypot(vx,vy)>0.01 else None
    else:return None
    stamp=struct.unpack_from('<Q',body,len(body)-8)[0]
    if abs(stamp-clock_offset_ms-capture_ms)>5000 or not all(math.isfinite(v) for v in (x,y,z,face)):
        return None
    if abs(x)>=420000 or abs(y)>=420000 or abs(z)>=120000 or abs(face)>360:return None
    return dict(x=x,y=y,z=z,faceYaw=face,moveYaw=move,stopped=stopped,t=capture_ms,op=op.hex().upper())

class FrameStream:
    """Reorder bounded TCP fragments; only a persistent gap invalidates cipher alignment."""
    GAP_TIMEOUT_MS=1500
    MAX_PENDING_BYTES=256*1024
    MAX_PENDING_SEGMENTS=256
    def __init__(self,on_frame,on_reset):
        self.on_frame=on_frame;self.on_reset=on_reset;self.next=None;self.buffer=bytearray()
        self.pending=[];self.pending_bytes=0;self.out_of_order=0;self.tcp_gaps=0
    @staticmethod
    def diff(seq,expected):return ((seq-expected+2**31)%2**32)-2**31
    def push(self,seq,data,t):
        if not data:return
        if self.next is None:self.next=seq
        diff=self.diff(seq,self.next)
        if diff<0:
            if -diff>=len(data):return
            data=data[-diff:];seq=self.next;diff=0
        if diff>0:
            self.out_of_order+=1
            if not any(s==seq and len(d)>=len(data) for s,d,_ in self.pending):
                self.pending.append((seq,bytes(data),t));self.pending_bytes+=len(data)
        else:
            self.deliver(data,t);self.drain()
        self.expire(t)
    def expire(self,t):
        while self.pending and (self.pending_bytes>self.MAX_PENDING_BYTES or
              len(self.pending)>self.MAX_PENDING_SEGMENTS or
              t-min(stamp for _,_,stamp in self.pending)>self.GAP_TIMEOUT_MS):
            self.next=min(self.pending,key=lambda item:self.diff(item[0],self.next))[0]
            self.buffer.clear();self.tcp_gaps+=1;self.on_reset();self.drain()
    def drain(self):
        while True:
            for i,(seq,data,t) in enumerate(self.pending):
                diff=self.diff(seq,self.next)
                if diff>0:continue
                self.pending.pop(i);self.pending_bytes-=len(data)
                if -diff<len(data):self.deliver(data[-diff:] if diff else data,t)
                break
            else:return
    def deliver(self,data,t):
        self.next=(self.next+len(data))%2**32;self.buffer.extend(data)
        while self.buffer:
            n=0;width=0;valid=False
            for i,b in enumerate(self.buffer[:5]):
                n|=(b&127)<<(7*i);width=i+1
                if not b&128:valid=True;break
            if not valid:
                if len(self.buffer)<5:break
                self.buffer.pop(0);self.on_reset();continue
            if n<6 or n>65535:
                self.buffer.pop(0);self.on_reset();continue
            size=width+n-4
            if len(self.buffer)<size:break
            body=bytes(self.buffer[width:size]);del self.buffer[:size]
            self.on_frame(t,body)
        if len(self.buffer)>65540:self.buffer.clear();self.on_reset()
