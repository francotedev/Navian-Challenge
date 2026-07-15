using System;
using UnityEngine;

namespace NavianChallenge
{
    /// <summary>The active tool / interaction mode. Systems branch on this instead of
    /// each grabbing the mouse independently, which keeps input unambiguous.</summary>
    public enum Tool
    {
        Explore,        // orbit / inspect only
        Slices,         // MPR crosshair (built later)
        PlanTrajectory, // place target + entry, safety corridor (built later)
        Measure,        // two-point distance (built later)
        Craniotomy      // cross-section box (built later)
    }

    /// <summary>
    /// Central source of truth for the active <see cref="Tool"/>. This is the "regla de oro"
    /// from the design brief: one place holds the mode, every interaction system reads from
    /// here, so we never have two systems fighting over the same click.
    /// </summary>
    public class AppState : MonoBehaviour
    {
        /// <summary>Convenience accessor. The bootstrap creates exactly one AppState.</summary>
        public static AppState Instance { get; private set; }

        [SerializeField] Tool current = Tool.Explore;

        /// <summary>Raised whenever the active tool changes (new tool passed as argument).</summary>
        public event Action<Tool> ToolChanged;

        public Tool Current => current;

        public void SetTool(Tool tool)
        {
            if (current == tool) return;
            current = tool;
            ToolChanged?.Invoke(current);
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
