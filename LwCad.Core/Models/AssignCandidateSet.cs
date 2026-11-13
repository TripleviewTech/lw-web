using System.Collections.Generic;

namespace LwCadSystemUX.Models
{
    /// <summary>
    /// What an assign screen offers for a call, and on what basis it was arrived at.
    ///
    /// The basis matters as much as the list. A recommendation is the units that
    /// cover the call - if none do and the screen has fallen back to a wider slice
    /// of the roster, the dispatcher has to be told, or they will read a widened
    /// list as a recommendation and send the wrong unit.
    /// </summary>
    public class AssignCandidateSet
    {
        public AssignCandidateSet(IReadOnlyList<AssignGroup> groups, string basis, bool isWidened)
        {
            Groups = groups ?? new List<AssignGroup>();
            Basis = basis ?? string.Empty;
            IsWidened = isWidened;
        }

        public IReadOnlyList<AssignGroup> Groups { get; }

        /// <summary>One line saying how these units were chosen.</summary>
        public string Basis { get; }

        /// <summary>True when nothing covered the call and the screen widened its search.</summary>
        public bool IsWidened { get; }

        public static AssignCandidateSet Empty { get; } =
            new AssignCandidateSet(new List<AssignGroup>(), string.Empty, false);
    }
}
