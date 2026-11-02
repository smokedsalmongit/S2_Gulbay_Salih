using Oculus.Interaction;
using UnityEngine;

namespace SaturdayStudio
{
    /// <summary>Is someone holding this object right now, with a VR hand/controller or in desktop test mode?</summary>
    public static class GrabState
    {
        public static bool IsHeld(Rigidbody rb)
        {
            if (rb == null) return false;
            if (DesktopFallbackPlayer.HeldBody == rb) return true;
            var grabbable = rb.GetComponent<Grabbable>();
            return grabbable != null && grabbable.SelectingPointsCount > 0;
        }
    }
}
