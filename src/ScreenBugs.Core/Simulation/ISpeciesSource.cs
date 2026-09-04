namespace ScreenBugs.Core.Simulation;

/// <summary>Decides which species each new bug is. Lets the simulation stay ignorant of the options.</summary>
public interface ISpeciesSource
{
    BugSpecies Next();
}
