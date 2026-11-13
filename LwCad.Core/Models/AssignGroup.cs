using System.Collections.ObjectModel;

namespace LwCadSystemUX.Models
{
    /// <summary>
    /// A labelled row of candidates on an assign screen. LwCad132 and LwCad133 lay
    /// the roster out one apparatus type per row - Chief, Pumper, Aer/Ladder,
    /// Ambulance, EMT - so the group carries that heading. LwCad131 and LwCad134
    /// have no headings: they hand back a single unnamed group, which the panel
    /// renders as the numbered slot grid those screens use.
    /// </summary>
    public class AssignGroup
    {
        public AssignGroup(string name)
        {
            Name = name ?? string.Empty;
        }

        public string Name { get; }

        public bool HasName => Name.Length > 0;

        public ObservableCollection<AssignCandidate> Candidates { get; } =
            new ObservableCollection<AssignCandidate>();
    }
}
