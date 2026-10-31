"""Exact-connection, outgoing-only Npcap wrapper. Never sends/injects packets."""
import ctypes as c
from ctypes import wintypes as w
import os
import socket
import struct
from read_only_locator import MIB_TCPROW_OWNER_PID

def connection_for(pid):
    api=c.WinDLL('iphlpapi',use_last_error=True)
    fn=api.GetExtendedTcpTable;fn.argtypes=[c.c_void_p,c.POINTER(w.DWORD),w.BOOL,w.ULONG,w.DWORD,w.ULONG];fn.restype=w.DWORD
    size=w.DWORD();fn(None,c.byref(size),False,2,5,0)
    buf=c.create_string_buffer(size.value)
    if fn(buf,c.byref(size),False,2,5,0):raise OSError('TCP tablosu okunamadı.')
    for index in range(struct.unpack_from('<I',buf.raw)[0]):
        row=MIB_TCPROW_OWNER_PID.from_buffer_copy(buf.raw,4+index*c.sizeof(MIB_TCPROW_OWNER_PID))
        port=int.from_bytes(struct.pack('<I',row.dwRemotePort)[:2],'big')
        if row.dwOwningPid==pid and row.dwState==5 and port==13328:
            return (socket.inet_ntoa(struct.pack('<I',row.dwLocalAddr)),int.from_bytes(struct.pack('<I',row.dwLocalPort)[:2],'big'),
                    socket.inet_ntoa(struct.pack('<I',row.dwRemoteAddr)),port)
    return None

class Addr(c.Structure):pass
Addr._fields_=[('next',c.POINTER(Addr)),('addr',c.c_void_p),('mask',c.c_void_p),('broad',c.c_void_p),('dst',c.c_void_p)]
class Dev(c.Structure):pass
Dev._fields_=[('next',c.POINTER(Dev)),('name',c.c_char_p),('description',c.c_char_p),('addresses',c.POINTER(Addr)),('flags',c.c_uint)]
class Header(c.Structure):
    _fields_=[('sec',c.c_long),('usec',c.c_long),('caplen',c.c_uint),('length',c.c_uint)]
class Bpf(c.Structure):_fields_=[('length',c.c_uint),('instructions',c.c_void_p)]

class Pcap:
    def __init__(self,connection):
        kernel=c.WinDLL('kernel32');kernel.GetSystemDirectoryW.argtypes=[w.LPWSTR,w.UINT];kernel.GetSystemDirectoryW.restype=w.UINT
        system=c.create_unicode_buffer(32768)
        if not kernel.GetSystemDirectoryW(system,len(system)):raise OSError('Windows sistem klasörü bulunamadı.')
        path=os.path.join(system.value,'Npcap')
        self.directory=os.add_dll_directory(path)
        self.api=c.CDLL(os.path.join(path,'wpcap.dll'))
        a=self.api
        a.pcap_findalldevs.argtypes=[c.POINTER(c.POINTER(Dev)),c.c_char_p];a.pcap_findalldevs.restype=c.c_int
        a.pcap_freealldevs.argtypes=[c.POINTER(Dev)]
        a.pcap_open_live.argtypes=[c.c_char_p,c.c_int,c.c_int,c.c_int,c.c_char_p];a.pcap_open_live.restype=c.c_void_p
        a.pcap_compile.argtypes=[c.c_void_p,c.POINTER(Bpf),c.c_char_p,c.c_int,c.c_uint];a.pcap_compile.restype=c.c_int
        a.pcap_setfilter.argtypes=[c.c_void_p,c.POINTER(Bpf)];a.pcap_setfilter.restype=c.c_int
        a.pcap_freecode.argtypes=[c.POINTER(Bpf)]
        a.pcap_next_ex.argtypes=[c.c_void_p,c.POINTER(c.POINTER(Header)),c.POINTER(c.POINTER(c.c_ubyte))];a.pcap_next_ex.restype=c.c_int
        a.pcap_close.argtypes=[c.c_void_p]
        a.pcap_datalink.argtypes=[c.c_void_p];a.pcap_datalink.restype=c.c_int
        error=c.create_string_buffer(512);devices=c.POINTER(Dev)();name=None
        if a.pcap_findalldevs(c.byref(devices),error)!=0:raise OSError('Npcap adaptör listesi okunamadı.')
        try:
            dev=devices
            while dev:
                address=dev.contents.addresses
                while address:
                    ptr=address.contents.addr
                    if ptr and c.c_ushort.from_address(ptr).value==2:
                        ip=socket.inet_ntoa(c.string_at(ptr+4,4))
                        if ip==connection[0]:name=dev.contents.name;break
                    address=address.contents.next
                if name:break
                dev=dev.contents.next
        finally:a.pcap_freealldevs(devices)
        if not name:raise OSError('Oyun bağlantısının Npcap adaptörü bulunamadı.')
        self.handle=a.pcap_open_live(name,65535,0,20,error)
        if not self.handle:raise OSError('Npcap dinlemesi açılamadı.')
        self.link=a.pcap_datalink(self.handle);self.connection=connection
        local,lp,remote,rp=connection
        program=Bpf()
        rule=f'tcp and src host {local} and src port {lp} and dst host {remote} and dst port {rp}'.encode('ascii')
        if a.pcap_compile(self.handle,c.byref(program),rule,1,0xffffffff)!=0:self.close();raise OSError('Npcap filtresi derlenemedi.')
        try:
            if a.pcap_setfilter(self.handle,c.byref(program))!=0:self.close();raise OSError('Npcap filtresi uygulanamadı.')
        finally:a.pcap_freecode(c.byref(program))
    def next(self):
        header=c.POINTER(Header)();data=c.POINTER(c.c_ubyte)()
        status=self.api.pcap_next_ex(self.handle,c.byref(header),c.byref(data))
        if status==0:return None
        if status<0:raise OSError('Npcap dinlemesi sona erdi.')
        h=header.contents
        if h.caplen>65535:return None
        raw=c.string_at(data,h.caplen)
        offset=14 if self.link==1 else 4 if self.link==0 else -1
        if offset<0 or len(raw)<offset+40:return None
        if self.link==1:
            kind=struct.unpack_from('>H',raw,12)[0]
            if kind==0x8100:
                if len(raw)<18:return None
                kind=struct.unpack_from('>H',raw,16)[0];offset=18
            if kind!=0x0800:return None
        ip=raw[offset:];ihl=(ip[0]&15)*4
        if ip[0]>>4!=4 or ihl<20 or len(ip)<ihl+20 or ip[9]!=6:return None
        if struct.unpack_from('>H',ip,6)[0]&0x3fff:return None
        total=struct.unpack_from('>H',ip,2)[0]
        if total>len(ip) or total<ihl+20:return None
        tcp=ip[ihl:total];thl=(tcp[12]>>4)*4
        if thl<20 or thl>len(tcp):return None
        local,lp,remote,rp=self.connection
        if (socket.inet_ntoa(ip[12:16]),struct.unpack_from('>H',tcp,0)[0],socket.inet_ntoa(ip[16:20]),struct.unpack_from('>H',tcp,2)[0])!=self.connection:return None
        return (int(h.sec)*1000+int(h.usec)//1000,struct.unpack_from('>I',tcp,4)[0],tcp[13],tcp[thl:])
    def close(self):
        if getattr(self,'handle',None):self.api.pcap_close(self.handle);self.handle=None
        if getattr(self,'directory',None):self.directory.close();self.directory=None
