"""Pose leases use actual validated world clocks, never UI/report receipt time."""


class PoseObservation:
    def __init__(self):
        self.generation = self.serial = 0
        self.latest = None
        self.position = None
        self.changed = 0

    def reset(self):
        self.generation += 1
        self.serial += 1
        self.latest = self.position = None
        self.changed = 0

    def motion(self, value):
        position = (value['x'], value['y'], value['z'])
        if position != self.position:
            self.changed = value['t']
        self.position = position
        self.serial += 1
        self.latest = dict(value, poseGeneration=self.generation, poseSerial=self.serial,
                           positionChangedAtMs=self.changed)
        return self.latest

    def heartbeat(self, clock_ms):
        if self.latest is None:
            return None
        pose = self.latest
        # Only an explicit decoded stop may retain a pose on subsequent clocks.
        observed = max(pose['t'], clock_ms) if pose['stopped'] else pose['t']
        return dict(motionAtMs=pose['t'], observationAtMs=observed,
                    positionChangedAtMs=self.changed, stopped=pose['stopped'],
                    generation=self.generation, serial=self.serial)
