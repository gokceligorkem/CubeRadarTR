"""Exact C2S selected-target state. No raw payload, input, memory writes or secrets."""


def parse_target_selection(body, stamp):
    if body[:2] != b'\x33\x38':
        return None
    unknown = dict(known=False, entityId=None, selectedAtMs=0)
    if len(body) < 4 or len(body) > 8 or body[2] != 0 or stamp <= 0:
        return unknown
    entity = 0
    for index, byte in enumerate(body[3:]):
        if index == 4 and byte > 15:
            return unknown
        entity |= (byte & 127) << (index * 7)
        if not byte & 128:
            if index + 4 != len(body) or index > 0 and byte == 0:
                return unknown
            return dict(known=True, entityId=entity, selectedAtMs=stamp)
    return unknown


class TargetSelectionState:
    def __init__(self):
        self.known = False
        self.entity = None
        self.selected_ms = 0
        self.generation = 0
        self.serial = 0

    def reset(self):
        self.known = False
        self.entity = None
        self.selected_ms = 0
        self.generation += 1
        self.serial += 1

    def observe(self, body, stamp):
        value = parse_target_selection(body, stamp)
        if value is None:
            return None
        if not value['known'] or stamp < self.selected_ms:
            self.reset()
        else:
            self.known = True
            self.entity = value['entityId']
            self.selected_ms = stamp
            self.serial += 1
        return self.snapshot()

    def snapshot(self):
        return dict(known=self.known, entityId=self.entity, selectedAtMs=self.selected_ms,
                    generation=self.generation, serial=self.serial)
