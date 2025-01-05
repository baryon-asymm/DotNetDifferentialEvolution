using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.Interfaces;

public interface IPopulationUpdatedHandler
{
    public void Handle(
        Population population);
}
