using Oculus.Interaction;
using UnityEngine;

namespace SaturdayStudio
{
    public interface IActivatable
    {
        string Label { get; }
        bool CanActivate { get; }
        void Activate();
    }

    /// <summary>
    /// Something you "click" to do a chore. In VR it uses the Interaction SDK RayInteractable on the same object
    /// (point with the controller ray and press the trigger, or pinch with a hand); in desktop mode E / left click.
    /// </summary>
    public class TaskClickable : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour _target;

        private RayInteractable _ray;

        public IActivatable Target => _target as IActivatable;

        public void SetTarget(MonoBehaviour target) => _target = target;

        private void OnEnable()
        {
            _ray = GetComponent<RayInteractable>();
            if (_ray != null) _ray.WhenPointerEventRaised += OnPointer;
        }

        private void OnDisable()
        {
            if (_ray != null) _ray.WhenPointerEventRaised -= OnPointer;
        }

        private void OnPointer(PointerEvent evt)
        {
            if (evt.Type == PointerEventType.Select) Target?.Activate();
        }
    }
}
