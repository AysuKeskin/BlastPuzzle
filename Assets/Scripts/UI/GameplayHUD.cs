using System.Collections.Generic;
using BlastPuzzle.Core;
using BlastPuzzle.Gameplay;
using BlastPuzzle.Goals;
using BlastPuzzle.Presentation;
using TMPro;
using UnityEngine;

namespace BlastPuzzle.UI
{
    // Displays the attempt. Reads gameplay state; never changes it.
    public sealed class GameplayHUD : MonoBehaviour
    {
        [SerializeField]
        private GameplayController gameplay;

        [Tooltip("Read for the level number, and asked whether a next level exists.")]
        [SerializeField]
        private GameFlowController flow;

        [SerializeField]
        private TMP_Text levelText;

        [SerializeField]
        private TMP_Text movesText;

        [Tooltip("Goal rows are created as children of this, one per runtime goal.")]
        [SerializeField]
        private RectTransform goalsContainer;

        [SerializeField]
        private GoalRowView goalRowPrefab;

        [SerializeField]
        private GameObject winPanel;

        [SerializeField]
        private GameObject losePanel;

        [Tooltip("Reads 'LEVEL COMPLETE', or something final after the last level.")]
        [SerializeField]
        private TMP_Text winTitleText;

        [Tooltip("The win panel's primary button. Always shown; only its label changes.")]
        [SerializeField]
        private GameObject nextLevelButton;

        [SerializeField]
        private TMP_Text nextLevelLabel;

        [SerializeField]
        private string winTitle = "LEVEL COMPLETE";

        [SerializeField]
        private string finalWinTitle = "ALL LEVELS COMPLETE";

        [SerializeField]
        private string nextLevelLabelText = "NEXT LEVEL";

        [Tooltip("Shown instead after the final level, where the button restarts the sequence.")]
        [SerializeField]
        private string replayLabelText = "PLAY AGAIN";

        private readonly List<GoalRowView> goalRows = new List<GoalRowView>();
        private GoalTracker rowsBuiltFor;
        [SerializeField] private BoardView boardView;
        private TMP_Text retryTitle;
        private string defaultRetryTitle;

        private void Awake()
        {
            // Compatibility for existing scenes; the reference can also be wired explicitly.
            if (boardView == null) boardView = FindAnyObjectByType<BoardView>();
            if (boardView != null) boardView.SetHudBoundary(goalsContainer);
            // Reuse the authored retry panel for a stalled board, including existing scenes.
            foreach (TMP_Text text in losePanel.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name != "TitleText") continue;
                retryTitle = text;
                defaultRetryTitle = text.text;
                break;
            }
        }
        private void OnEnable()
        {
            gameplay.GameplayChanged += HandleGameplayChanged;
            gameplay.StateChanged += HandleStateChanged;
            Refresh();
            ApplyState(gameplay.State);
        }

        private void OnDisable()
        {
            gameplay.GameplayChanged -= HandleGameplayChanged;
            gameplay.StateChanged -= HandleStateChanged;
        }

        private void HandleGameplayChanged() => Refresh();

        private void HandleStateChanged(GameplayState state)
        {
            Refresh();
            ApplyState(state);
        }

        private void Refresh()
        {
            if (gameplay.Goals == null)
            {
                // No attempt composed yet.
                return;
            }

            if (flow != null && flow.CurrentLevel != null)
            {
                levelText.text = $"Level {flow.CurrentLevel.LevelNumber}";
            }
            movesText.text = gameplay.MovesRemaining.ToString();

            RefreshGoals(gameplay.Goals);
        }

        private void RefreshGoals(GoalTracker goals)
        {
            if (!ReferenceEquals(goals, rowsBuiltFor))
            {
                RebuildGoalRows(goals);
            }

            int row = 0;

            foreach (ColorGoal goal in goals.Goals)
            {
                goalRows[row++].ShowColorGoal(goal);
            }

            if (goals.CrateGoal != null)
            {
                goalRows[row].ShowCrateGoal(goals.CrateGoal);
            }
        }

        private void RebuildGoalRows(GoalTracker goals)
        {
            foreach (GoalRowView existing in goalRows)
            {
                Destroy(existing.gameObject);
            }

            goalRows.Clear();

            int rowCount = goals.Goals.Count + (goals.CrateGoal != null ? 1 : 0);

            for (int i = 0; i < rowCount; i++)
            {
                goalRows.Add(Instantiate(goalRowPrefab, goalsContainer));
            }

            rowsBuiltFor = goals;
        }
        private void ApplyState(GameplayState state)
        {
            bool won = state == GameplayState.Won;
            bool blocked = state == GameplayState.Blocked;
            bool lost = state == GameplayState.Lost || blocked;
            if (retryTitle != null)
                retryTitle.text = blocked ? "NO MATCHES — RETRY" : defaultRetryTitle;

            winPanel.SetActive(won);
            losePanel.SetActive(lost);

            if (!won)
            {
                return;
            }
            bool hasNext = flow != null && flow.HasNextLevel;
            nextLevelButton.SetActive(true);
            nextLevelLabel.text = hasNext ? nextLevelLabelText : replayLabelText;
            winTitleText.text = hasNext ? winTitle : finalWinTitle;
        }
    }
}
