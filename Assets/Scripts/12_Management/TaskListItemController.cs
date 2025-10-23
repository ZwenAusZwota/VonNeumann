using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SpaceGame.Mining;

namespace SpaceGame.UI
{
    /// <summary>
    /// Repräsentiert einen Listeneintrag im TaskPanel (reine Anzeige + Delete).
    /// </summary>
    public class TaskListItemController : MonoBehaviour
    {
        [SerializeField] TMP_Text lblTitle;
        [SerializeField] TMP_Text lblDetails;
        [SerializeField] Button btnDelete;

        MiningTask _task;
        Action<MiningTask> _onDelete;

        public void Bind(MiningTask task, Action<MiningTask> onDelete)
        {
            _task = task;
            _onDelete = onDelete;
            if (lblTitle) lblTitle.text = task.Name;
            if (lblDetails) lblDetails.text = task.ToString();

            if (btnDelete != null)
            {
                btnDelete.onClick.RemoveAllListeners();
                btnDelete.onClick.AddListener(() => _onDelete?.Invoke(_task));
            }
        }
    }
}
