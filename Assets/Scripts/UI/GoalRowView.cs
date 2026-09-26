using BlastPuzzle.Goals;
using BlastPuzzle.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlastPuzzle.UI
{
    // One goal chip: the block's own art, its name and the progress.
    public sealed class GoalRowView : MonoBehaviour
    {
        [Tooltip("Shows the actual block art, so the chip and the board cannot disagree.")]
        [SerializeField]
        private Image icon;

        [SerializeField]
        private BlockSpriteSet sprites;

        [SerializeField]
        private TMP_Text labelText;

        [SerializeField]
        private TMP_Text progressText;

        [Tooltip("Applied to a finished row so completed goals read as done at a glance.")]
        [SerializeField]
        private Color completedTextColor = new Color(0.55f, 0.85f, 0.55f);

        [SerializeField]
        private Color activeTextColor = Color.white;
        public void ShowColorGoal(ColorGoal goal)
        {
            Apply(
                sprites.For(goal.Color),
                goal.Color.ToString(),
                goal.CurrentCount,
                goal.TargetCount,
                goal.IsComplete);
        }

        public void ShowCrateGoal(CrateGoal goal)
        {
            Apply(sprites.Crate, "Crates", goal.CurrentCount, goal.TargetCount, goal.IsComplete);
        }

        private void Apply(Sprite art, string label, int current, int target, bool complete)
        {
            icon.sprite = art;
            icon.color = Color.white;
            labelText.text = label;
            progressText.text = complete ? $"{current} / {target}  DONE" : $"{current} / {target}";

            Color textColor = complete ? completedTextColor : activeTextColor;
            labelText.color = textColor;
            progressText.color = textColor;
        }
    }
}
