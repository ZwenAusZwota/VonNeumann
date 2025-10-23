using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SpaceGame.Mining;

namespace SpaceGame.UI
{
    /// <summary>
    /// Steuert das HUD-Panel "Tasks": Formular zum Erstellen + Liste darunter.
    /// </summary>
    public class TaskPanelController : MonoBehaviour
    {
        [Header("Form Felder")]
        [SerializeField] GameObject inputForm;
        [SerializeField] TMP_InputField inputName;
        [SerializeField] TMP_Dropdown dropdownMode;        // Any/Specific
        [SerializeField] TMP_Dropdown dropdownRegion;      // Any/Belt/Gas
        [SerializeField] TMP_Dropdown dropdownDropoff;     // aus Szene
        [SerializeField] Toggle toggleLoop;
        [SerializeField] TMP_InputField inputScanRadius;   // float
        [SerializeField] TMP_InputField inputRescan;       // float
        [SerializeField] TMP_InputField inputPreferredMiners; // int

        [Header("Resources Auswahl (nur bei Specific)")]
        [SerializeField] Transform resourcesContainer; // Parent mit Toggle-Elementen
        [SerializeField] Toggle resourceTogglePrefab;  // Toggle mit Label

        [Header("Liste")]
        [SerializeField] Transform listContainer;      // VerticalLayout
        [SerializeField] TaskListItemController listItemPrefab;

        [Header("Buttons")]
        [SerializeField] Button btnCreate;
        [SerializeField] Button btnClose;
        [SerializeField] Button btnAdd;


        // interner Index: Dropoff-Einträge
        readonly List<Transform> _dropoffTargets = new();

        void OnEnable()
        {
            // Dropdowns initialisieren, wenn Panel geöffnet wird
            BuildModeDropdown();
            BuildRegionDropdown();
            BuildDropoffDropdown();
            BuildResourceChecklist();

            if (MiningTaskManager.Instance != null)
                MiningTaskManager.Instance.TasksChanged += RefreshList;

            RefreshList();

            btnAdd.onClick.AddListener(() => inputForm.SetActive(true));
            btnCreate.onClick.AddListener(CreateTaskFromForm);
            btnClose.onClick.AddListener(CloseForm);
            dropdownMode.onValueChanged.AddListener(_ => UpdateResourcesVisibility());
            UpdateResourcesVisibility();
        }

        void OnDisable()
        {
            if (MiningTaskManager.Instance != null)
                MiningTaskManager.Instance.TasksChanged -= RefreshList;

            btnAdd.onClick.RemoveAllListeners();
            btnCreate.onClick.RemoveAllListeners();
            btnClose.onClick.RemoveAllListeners();
            dropdownMode.onValueChanged.RemoveAllListeners();
        }

        void BuildModeDropdown()
        {
            dropdownMode.ClearOptions();
            dropdownMode.AddOptions(new List<string> { "Beliebig", "Spezifisch" });
            dropdownMode.value = 0;
        }

        void BuildRegionDropdown()
        {
            dropdownRegion.ClearOptions();
            dropdownRegion.AddOptions(new List<string> { "Any", "AsteroidBelt", "GasGiant" });
            dropdownRegion.value = 1; // häufigster Fall
        }

        void BuildDropoffDropdown()
        {
            dropdownDropoff.ClearOptions();
            _dropoffTargets.Clear();

            var options = HubRegistry.Instance?.GetOptions();
            var labels = new List<string>();

            if (options != null && options.Count > 0)
            {
                foreach (var (id, label) in options)
                {
                    labels.Add(label);
                    // Im Management-Screen haben wir keine echten Transforms:
                    // Stattdessen speichern wir die Hub-ID in einer parallelen Liste als "Fake-Transform".
                    // Lösung: wir legen ein Dummy-Transform NICHT an, sondern speichern die ID temporär.
                    // -> Ergänze unten: _dropoffHubIds parallel tracken.
                }
            }
            else
            {
                labels.Add("<kein Hub registriert>");
            }

            dropdownDropoff.AddOptions(labels);
            dropdownDropoff.value = 0;
        }

        void BuildResourceChecklist()
        {
            // Alte Toggles entfernen
            foreach (Transform c in resourcesContainer) Destroy(c.gameObject);

            foreach (var kind in System.Enum.GetValues(typeof(ResourceKind)))
            {
                var t = Instantiate(resourceTogglePrefab, resourcesContainer);
                var label = t.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = kind.ToString();
                t.isOn = false;
                t.gameObject.name = $"Res-{kind}";
                // Speichere das Enum auf dem Toggle
                t.gameObject.AddComponent<ResourceKindHolder>().Kind = (ResourceKind)kind;
            }
        }

        void UpdateResourcesVisibility()
        {
            bool specific = dropdownMode.value == 1;
            resourcesContainer.gameObject.SetActive(specific);
        }

        void CreateTaskFromForm()
        {
            if (MiningTaskManager.Instance == null) return;

            string name = inputName != null ? inputName.text : "";
            var mode = dropdownMode.value == 0 ? SearchMode.Any : SearchMode.Specific;
            var region = dropdownRegion.value switch
            {
                0 => RegionType.Any,
                1 => RegionType.AsteroidBelt,
                2 => RegionType.GasGiant,
                _ => RegionType.Any
            };

            var wanted = new List<ResourceKind>();
            if (mode == SearchMode.Specific)
            {
                foreach (Transform c in resourcesContainer)
                {
                    var tgl = c.GetComponent<Toggle>();
                    var holder = c.GetComponent<ResourceKindHolder>();
                    if (tgl != null && holder != null && tgl.isOn)
                        wanted.Add(holder.Kind);
                }
            }

            Transform dropoff = null;
            if (_dropoffTargets.Count > 0)
            {
                int ix = Mathf.Clamp(dropdownDropoff.value, 0, _dropoffTargets.Count - 1);
                dropoff = _dropoffTargets[ix];
            }

            float scanRadius = ParseFloat(inputScanRadius, 100000f);
            float rescan = ParseFloat(inputRescan, 15f);
            int miners = ParseInt(inputPreferredMiners, 1);
            bool loop = toggleLoop != null ? toggleLoop.isOn : true;

            //var task = MiningTaskManager.Instance.CreateTask(
                //name, mode, region, wanted, dropoff, loop, scanRadius, rescan, miners);
            var (hubId, hubLabel) = HubRegistry.Instance.GetOptions()[dropdownDropoff.value];
            var task = MiningTaskManager.Instance.CreateTask(
                name, mode, region, wanted, null /*DropoffHub*/, loop, scanRadius, rescan, miners);
            task.DropoffHubId = hubId;

            // Formular zurücksetzen (optional)
            if (inputName) inputName.text = "";
            inputForm.SetActive(false);
        }

        void CloseForm()
        {
            // Formular zurücksetzen (optional)
            if (inputName) inputName.text = "";
            inputForm.SetActive(false);
        }

        float ParseFloat(TMP_InputField field, float fallback)
            => (field != null && float.TryParse(field.text, out var v)) ? v : fallback;

        int ParseInt(TMP_InputField field, int fallback)
            => (field != null && int.TryParse(field.text, out var v)) ? v : fallback;

        void RefreshList()
        {
            if (MiningTaskManager.Instance == null) return;

            // Children löschen
            foreach (Transform c in listContainer) Destroy(c.gameObject);

            var tasks = MiningTaskManager.Instance.GetAll();
            foreach (var t in tasks)
            {
                var item = Instantiate(listItemPrefab, listContainer);
                item.Bind(t, OnClickDelete);
            }
        }

        void OnClickDelete(MiningTask t)
        {
            MiningTaskManager.Instance.RemoveTask(t.TaskId);
        }
    }

    /// <summary>
    /// Kleines Helferlein, um das Enum am Toggle zu speichern.
    /// </summary>
    public class ResourceKindHolder : MonoBehaviour
    {
        public ResourceKind Kind;
    }
}
