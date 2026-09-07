using Greyline.Core;
using Greyline.Enemies;
using UnityEngine;

namespace Greyline.Slice
{
    /// <summary>
    /// Slice01-only gate for the service-alley encounter. It combines one existing bool flag
    /// with one local trigger; it is not a reusable quest or trigger framework.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SliceEncounterGate : MonoBehaviour
    {
        private const string DefaultStudentMetFlag = "slice.student.greeted";

        [SerializeField] private EnemyAttack enemyAttack;
        [SerializeField] private Transform player;
        [SerializeField] private string studentMetFlag = DefaultStudentMetFlag;

        private GameFlagsHost flagsHost;
        private bool enteredAlley;
        private bool opened;

        public bool IsOpened => opened;
        public bool HasEnteredAlley => enteredAlley;
        public string StudentMetFlag => studentMetFlag;

        public void Configure(EnemyAttack attack, Transform target, string requiredFlag = DefaultStudentMetFlag)
        {
            enemyAttack = attack;
            player = target;
            studentMetFlag = string.IsNullOrWhiteSpace(requiredFlag) ? DefaultStudentMetFlag : requiredFlag;
            if (!opened && enemyAttack != null)
            {
                enemyAttack.enabled = false;
            }
        }

        private void Awake()
        {
            enemyAttack ??= GetComponentInChildren<EnemyAttack>();
            if (!opened && enemyAttack != null)
            {
                enemyAttack.enabled = false;
            }
        }

        private void OnEnable()
        {
            flagsHost = GameFlagsHost.Current;
            Subscribe();
            TryOpen();
        }

        private void Start()
        {
            if (flagsHost == null)
            {
                flagsHost = GameFlagsHost.Current ?? FindFirstObjectByType<GameFlagsHost>();
                Subscribe();
            }

            TryOpen();
        }

        private void OnDisable()
        {
            if (flagsHost != null)
            {
                flagsHost.Flags.FlagChanged -= OnFlagChanged;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (player != null && (other.transform == player || other.transform.IsChildOf(player)))
            {
                enteredAlley = true;
                TryOpen();
            }
        }

        private void Subscribe()
        {
            if (flagsHost != null)
            {
                flagsHost.Flags.FlagChanged -= OnFlagChanged;
                flagsHost.Flags.FlagChanged += OnFlagChanged;
            }
        }

        private void OnFlagChanged(string key, bool value)
        {
            if (key == studentMetFlag && value)
            {
                TryOpen();
            }
        }

        private void TryOpen()
        {
            if (opened || !enteredAlley || enemyAttack == null || flagsHost == null ||
                !flagsHost.Flags.Get(studentMetFlag))
            {
                return;
            }

            opened = true;
            enemyAttack.enabled = true;
            Debug.Log("[Slice01] Student met and service alley entered — encounter active.", this);
        }
    }
}
