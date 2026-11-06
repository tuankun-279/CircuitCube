using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CircuitCube.UI
{
    public sealed class ShapesScreen : UIScreen
    {
        public Button BackButton;
        public TMP_Text StarsLabel;
        public RectTransform List;
        public ShapeCard CardPrefab;
    }
}
