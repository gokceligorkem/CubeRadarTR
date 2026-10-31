import unittest
from target_wire import TargetSelectionState, parse_target_selection


def varint(value):
    data = bytearray()
    while value >= 128:
        data.append((value & 127) | 128)
        value >>= 7
    return bytes(data + bytes([value]))


class TargetWireTests(unittest.TestCase):
    def test_select_and_clear_require_the_exact_selection_opcode_and_complete_varint(self):
        for entity in [0, 123, 128, 395292, 0xffffffff]:
            with self.subTest(entity=entity):
                value = parse_target_selection(b'\x33\x38\0' + varint(entity), 10000)
                self.assertTrue(value['known'])
                self.assertEqual(value['entityId'], entity)
                self.assertEqual(value['selectedAtMs'], 10000)
        self.assertIsNone(parse_target_selection(b'\x30\x38\0\0', 10000))
        for body in [b'\x33\x38', b'\x33\x38\1\0', b'\x33\x38\0\x80',
                     b'\x33\x38\0\x80\0', b'\x33\x38\0\xff\xff\xff\xff\x10', b'\x33\x38\0\1\0']:
            with self.subTest(body=body):
                self.assertFalse(parse_target_selection(body, 10000)['known'])

    def test_selection_is_state_not_the_temporary_cast_context(self):
        state = TargetSelectionState()
        self.assertFalse(state.snapshot()['known'])
        state.observe(b'\x33\x38\0' + varint(395292), 10000)
        original = state.snapshot()
        self.assertIsNone(state.observe(b'\x30\x38\0\0', 11000))
        self.assertEqual(state.snapshot(), original)
        self.assertEqual(state.snapshot()['selectedAtMs'], 10000)
        state.observe(b'\x33\x38\0\0', 12000)
        self.assertTrue(state.snapshot()['known'])
        self.assertEqual(state.snapshot()['entityId'], 0)

    def test_gap_invalidates_selection_and_changes_the_stream_generation(self):
        state = TargetSelectionState()
        state.observe(b'\x33\x38\0' + varint(123), 10000)
        generation = state.snapshot()['generation']
        state.reset()
        self.assertFalse(state.snapshot()['known'])
        self.assertIsNone(state.snapshot()['entityId'])
        self.assertGreater(state.snapshot()['generation'], generation)

    def test_malformed_selection_never_retains_the_previous_target(self):
        state = TargetSelectionState()
        state.observe(b'\x33\x38\0' + varint(123), 10000)
        state.observe(b'\x33\x38\xff\1', 10100)
        self.assertFalse(state.snapshot()['known'])
        self.assertIsNone(state.snapshot()['entityId'])


if __name__ == '__main__':
    unittest.main()
