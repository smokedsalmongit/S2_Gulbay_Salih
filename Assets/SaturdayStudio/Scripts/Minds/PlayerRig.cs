using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>Moves the player (VR rig or desktop body) between the introduction room and the apartment.</summary>
    public static class PlayerRig
    {
        /// <summary>Default start in the procedural room: in front of the TV, looking at it.</summary>
        private static readonly Vector3 DefaultSpawn = new Vector3(1.2f, 0f, -0.45f);
        private const float DefaultYaw = 180f;

        public static void ToApartment()
        {
            var spawn = GameObject.Find("Player Spawn");
            if (spawn != null) TeleportTo(spawn.transform.position, spawn.transform.eulerAngles.y);
            else TeleportTo(DefaultSpawn, DefaultYaw);
        }

        /// <summary>Puts the player's feet at <paramref name="floor"/>, looking towards <paramref name="yaw"/> degrees.</summary>
        public static void TeleportTo(Vector3 floor, float yaw)
        {
            if (DesktopFallbackPlayer.Active)
            {
                DesktopFallbackPlayer.TeleportTo(floor, yaw);
                return;
            }
            Transform rig = null;
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name.Contains("Camera Rig")) { rig = root.transform; break; }
            if (rig == null) return;

            var head = rig.gameObject.activeInHierarchy ? MindInput.Head : null;
            if (head != null && head.IsChildOf(rig))
            {
                // turn the rig around the head so the head faces the new direction, then move the head over the spot
                rig.RotateAround(head.position, Vector3.up, Mathf.DeltaAngle(head.eulerAngles.y, yaw));
                var offset = head.position - rig.position;
                offset.y = 0;
                rig.position = new Vector3(floor.x, floor.y, floor.z) - offset;
            }
            else
            {
                rig.SetPositionAndRotation(floor, Quaternion.Euler(0, yaw, 0));
            }
        }
    }
}
