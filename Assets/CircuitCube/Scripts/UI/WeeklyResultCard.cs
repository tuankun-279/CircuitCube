using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CircuitCube.UI
{
    /// <summary>
    /// The win card of a weekly challenge level: NEW BEST, moves and time with the old best, the rank box (or the
    /// offline note in its place), then the next level, Try again and All levels.
    /// </summary>
    public sealed class WeeklyResultCard : MonoBehaviour
    {
        public Animation Anim;          // clip "In"
        public TMP_Text Caption, Title;
        public GameObject NewBest;
        public TMP_Text MovesValue, MovesWas, TimeValue, TimeWas;
        public Button RankBox;
        public TMP_Text RankLine, RankUp;
        public GameObject Offline;
        public TMP_Text OfflineText;
        public Button Next, TryAgain, AllLevels;
        public TMP_Text NextLabel;

        public void Open()
        {
            gameObject.SetActive(true);
            if (Anim != null && Anim.GetClip("In") != null) { Anim.Stop(); Anim.Play("In"); }
        }
    }
}
