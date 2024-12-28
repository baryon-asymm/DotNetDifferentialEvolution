using DotNetDifferentialEvolution.Models;

namespace DotNetDifferentialEvolution.Interfaces;

public interface IPopulationUpdateHandler
{
    public void Handle(
        Population population);
}
