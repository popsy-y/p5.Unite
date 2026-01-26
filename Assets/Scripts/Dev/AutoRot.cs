using UnityEngine;

namespace Dev
{
    public class AutoRot : MonoBehaviour
    {
        [SerializeField] private Vector3 axisSpeed;
        
        private void Update()
        {
            transform.Rotate(axisSpeed * Time.deltaTime);
        }
    }
}