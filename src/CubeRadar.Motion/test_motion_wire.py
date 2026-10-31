import struct
import unittest
from motion_wire import Rc4, find_alignment, parse_motion, FrameStream, recover_cursor

def frame(body):
    n=len(body)+4; out=bytearray()
    while n>=128:out.append((n&127)|128);n>>=7
    return bytes(out+bytes([n]))+body

class WireTests(unittest.TestCase):
    def test_flight_continuation_and_both_transitions(self):
        now=1793309000000
        fields=struct.pack('<fffffff',20000,30000,5000,100,200,300,-45)
        update=b'\x0b\x37'+fields+b'\x01'+struct.pack('<Q',now)
        transitions=[b'\x0a\x37\x02'+fields+b'\x01'+struct.pack('<Q',now),
                     b'\x0a\x37\x03'+fields+b'\x03\x01'+struct.pack('<Q',now)]
        for body in [update,*transitions]:
            with self.subTest(op=body[:3].hex()):
                value=parse_motion(body,now)
                self.assertIsNotNone(value)
                self.assertEqual((value['x'],value['y'],value['z']),(20000,30000,5000))
                self.assertEqual(value['faceYaw'],-45)
                self.assertAlmostEqual(value['moveYaw'],63.4349488,places=5)
                self.assertIsNone(parse_motion(body[:-1],now))
                self.assertIsNone(parse_motion(body,now+10000))
        self.assertIsNone(parse_motion(transitions[0][:2]+b'\x03'+transitions[0][3:],now))
        self.assertIsNone(parse_motion(update[:14]+struct.pack('<f',float('nan'))+update[18:],now))
        self.assertIsNone(parse_motion(update[:30]+b'\xff'+update[31:],now))

    def test_published_rc4_vector_and_inverse_steps(self):
        r=Rc4(b'Key'); before=r.clone()
        self.assertEqual(r.crypt(b'Plaintext').hex(),'bbf316e8d940af0ad3')
        for _ in range(9):r.back()
        self.assertEqual((r.s,r.i,r.j),(before.s,before.i,before.j))

    def test_live_state_aligns_without_session_restart(self):
        now=1793309000000;r=Rc4(bytes(range(214)))
        r.crypt(b'x'*10000)
        frames=[]
        for i in range(12):
            body=b'\x01\x36'+struct.pack('<Q',now+i*50)
            frames.append((now+i*50,r.crypt(body)))
        snapshot=r.clone();r.crypt(b'future data')
        aligned=find_alignment(snapshot,frames)
        self.assertIsNotNone(aligned)
        self.assertEqual(aligned.state.crypt(frames[aligned.index][1])[:2],b'\x01\x36')
        self.assertIsNone(find_alignment(Rc4(b'wrong'),frames))

    def test_ground_and_stop_fields_are_separate(self):
        now=1793309000000
        moving=b'\x01\x37\x02'+struct.pack('<fffHfB Q',20000,30000,1000,16384,-90,1,now)
        value=parse_motion(moving,now)
        self.assertEqual(value['moveYaw'],90)
        self.assertEqual(value['faceYaw'],-90)
        self.assertEqual(value['x'],20000)
        stop=moving[:2]+b'\x00'+moving[3:21]+moving[22:]
        self.assertEqual(len(stop),29)
        self.assertTrue(parse_motion(stop,now)['stopped'])
        self.assertIsNone(parse_motion(moving[:-1],now))
        self.assertIsNone(parse_motion(moving,now+100000))

    def test_clock_opcode_is_learned_from_timestamp_not_assumed(self):
        now=1793309000000;r=Rc4(bytes(range(214)))
        r.crypt(b'x'*3000);frames=[]
        for i in range(8):frames.append((now+i*50,r.crypt(b'\x03\x36'+struct.pack('<Q',now+i*50))))
        self.assertIsNotNone(find_alignment(r.clone(),frames))

    def test_independent_crypto_cursors_are_sampled_separately(self):
        from motion_reader import snapshot
        a=Rc4(b'key');b=a.clone().shift(500)
        def raw(r):
            data=bytearray(0x124);struct.pack_into('<II',data,0x1c,r.i,r.j);data[0x24:]=bytes(r.s);return bytes(data)
        class Fake:
            def read(self,address,size):return raw(a) if address==100 else raw(b)
        self.assertEqual(len(snapshot(Fake(),[100,200],0)),2)

    def test_runtime_cursor_convention_is_validated_against_key_and_full_permutation(self):
        r=Rc4(b'key').shift(512)
        memory=Rc4.snapshot(r.s,(r.i+1)%256,r.j)
        recovered=recover_cursor(b'key',memory,1024)
        self.assertIsNotNone(recovered)
        self.assertEqual((recovered.s,recovered.i,recovered.j),(r.s,r.i,r.j))

    def test_clock_sequence_can_validate_a_constant_client_clock_offset(self):
        now=1793309000000;r=Rc4(bytes(range(214)));frames=[]
        for i in range(8):frames.append((now+i*50,r.crypt(b'\x01\x36'+struct.pack('<Q',now+i*50+10800000))))
        aligned=find_alignment(r.clone(),frames)
        self.assertIsNotNone(aligned)
        self.assertEqual(aligned.offset_ms,10800000)

    def test_action_position_and_invalid_angles(self):
        now=1793309000000
        body=b'\x18\x37\x00\x00'+struct.pack('<ffffQ',20000,30000,1000,45,now)
        self.assertEqual(parse_motion(body,now)['faceYaw'],45)
        bad=body[:16]+struct.pack('<f',float('nan'))+body[20:]
        self.assertIsNone(parse_motion(bad,now))

    def test_frame_fragmentation_duplicates_and_gap_reset(self):
        frames=[];resets=[]
        stream=FrameStream(lambda t,b:frames.append(b),lambda:resets.append(1))
        raw=frame(b'\x01\x36'+b'x'*8)
        stream.push(100,raw[:5],1);stream.push(105,raw[5:],2)
        stream.push(100,raw,3)
        self.assertEqual(len(frames),1)
        stream.push(200,raw,4)
        stream.expire(2000)
        self.assertEqual(len(resets),1)
        self.assertEqual(len(frames),2)

    def test_reordered_tcp_fragments_preserve_the_cipher_and_continuous_ground_motion(self):
        now=1793309000000
        sender=Rc4(b'connection');receiver=Rc4(b'connection');decoded=[];resets=[]
        stream=FrameStream(lambda t,b:decoded.append(parse_motion(receiver.crypt(b),t)),lambda:resets.append(1))
        bodies=[b'\x01\x37\x02'+struct.pack('<fffHfBQ',20000+i*100,30000,1000,0,45+i,1,now) for i in range(4)]
        raw=b''.join(frame(sender.crypt(body)) for body in bodies)
        stream.push(100,raw[:12],now)
        stream.push(145,raw[45:80],now+10)
        stream.push(145,raw[45:80],now+11)  # duplicate while waiting
        stream.push(112,raw[12:45],now+20)
        stream.push(180,raw[80:],now+30)
        self.assertEqual(resets,[])
        self.assertEqual([v['faceYaw'] for v in decoded],[45,46,47,48])
        self.assertEqual([v['x'] for v in decoded],[20000,20100,20200,20300])

    def test_pending_tcp_segments_handle_wraparound_and_overlapping_retransmission(self):
        received=[];resets=[]
        stream=FrameStream(lambda t,b:received.append(b),lambda:resets.append(1))
        raw=frame(b'\x01\x36'+b'x'*8);start=2**32-5
        stream.push(start,raw[:3],1)
        stream.push((start+7)%2**32,raw[7:],2)
        stream.push((start+2)%2**32,raw[2:9],3)
        self.assertEqual(received,[b'\x01\x36'+b'x'*8])
        self.assertEqual(resets,[])

    def test_a_missing_segment_waits_then_invalidates_alignment_without_new_packets(self):
        received=[];resets=[]
        stream=FrameStream(lambda t,b:received.append(b),lambda:resets.append(1))
        raw=frame(b'\x01\x36'+b'x'*8)
        stream.push(100,raw,1000)
        stream.push(200,raw,1010)
        self.assertEqual(len(received),1)
        self.assertEqual(resets,[])
        stream.expire(2000)
        self.assertEqual(resets,[])
        stream.expire(2511)
        self.assertEqual(len(resets),1)
        self.assertEqual(received,[raw[1:],raw[1:]])

    def test_pending_capture_data_is_bounded_even_before_the_gap_timeout(self):
        stream=FrameStream(lambda t,b:None,lambda:None)
        raw=frame(b'\x01\x36'+b'x'*8)
        stream.push(100,raw,1)
        for i in range(300):stream.push(10000+i*100,raw,2)
        self.assertLessEqual(len(stream.pending),256)
        self.assertLessEqual(stream.pending_bytes,256*1024)

if __name__=='__main__':unittest.main()
