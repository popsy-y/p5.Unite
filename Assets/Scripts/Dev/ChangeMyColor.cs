using UnityEngine;

namespace Dev
{
    public class ChangeMyColor : MonoBehaviour
    {
        [SerializeField] private Color color;

        private static readonly int PropertyId = Shader.PropertyToID("_BaseColor");
        
        private void OnEnable()
        {
            var r = GetComponent<MeshRenderer>();

            if (!r) return;

            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(PropertyId, color);
            r.SetPropertyBlock(mpb);
        }
    }
}