import unittest
from pose_observation import PoseObservation


class PoseTests(unittest.TestCase):
    def test_stop_pose_has_distinct_change_and_observation_times(self):
        state = PoseObservation()
        moving = dict(x=100, y=200, z=300, t=10000, stopped=False)
        state.motion(moving)
        stop = state.motion(dict(moving, t=10100, stopped=True))
        value = state.heartbeat(14000)
        self.assertEqual(value['motionAtMs'], 10100)
        self.assertEqual(value['observationAtMs'], 14000)
        self.assertEqual(value['positionChangedAtMs'], 10000)
        self.assertEqual(stop['poseSerial'], value['serial'])

    def test_health_without_a_new_validated_clock_cannot_renew_a_pose(self):
        state = PoseObservation()
        state.motion(dict(x=100, y=200, z=300, t=10000, stopped=True))
        self.assertEqual(state.heartbeat(11000), state.heartbeat(11000))
        state.reset()
        self.assertIsNone(state.heartbeat(12000))

    def test_moving_or_unknown_pose_is_never_renewed_by_clock_alone(self):
        state = PoseObservation()
        state.motion(dict(x=100, y=200, z=300, t=10000, stopped=False))
        self.assertEqual(state.heartbeat(12000)['observationAtMs'], 10000)
        state.reset()
        self.assertIsNone(state.heartbeat(13000))

    def test_capture_reset_and_new_motion_invalidate_the_old_pose_identity(self):
        state = PoseObservation()
        old = state.motion(dict(x=100, y=200, z=300, t=10000, stopped=True))
        state.reset()
        new = state.motion(dict(x=101, y=200, z=300, t=12000, stopped=True))
        self.assertGreater(new['poseGeneration'], old['poseGeneration'])
        self.assertNotEqual(new['poseSerial'], old['poseSerial'])
        self.assertEqual(state.heartbeat(12500)['positionChangedAtMs'], 12000)
