using CubeRadar.Core.Radar;

namespace CubeRadar.Tests;

public class RadarOnlyBuildTests
{
    [Fact]
    public void Production_core_contains_no_automatic_hunt_engine_or_input_backend()
    {
        var types=typeof(RadarSnapshot).Assembly.GetTypes();
        Assert.DoesNotContain(types,t=>t.Namespace=="CubeRadar.Core.Hunting");
    }
}
