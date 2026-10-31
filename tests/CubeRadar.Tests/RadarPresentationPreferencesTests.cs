using System.Text.Json;
using CubeRadar.Core.Radar;
using CubeRadar.Tr;

namespace CubeRadar.Tests;

public class RadarPresentationPreferencesTests
{
    [Fact]
    public void Older_preferences_default_to_character_up_with_sonar_without_losing_existing_settings()
    {
        var preferences=JsonSerializer.Deserialize<UiPreferences>("{\"RangeM\":75,\"Volume\":42,\"Outside\":true}")!;
        Assert.Equal(RadarOrientationMode.CharacterUp,preferences.RadarOrientation);Assert.True(preferences.SonarEnabled);
        Assert.True(preferences.ShowCreatures);
        Assert.Equal(75,preferences.RangeM);Assert.Equal(42,preferences.Volume);Assert.True(preferences.Outside);
    }
    [Fact]
    public void Orientation_and_sonar_round_trip_as_ui_preferences_only()
    {
        var original=new UiPreferences {RadarOrientation=RadarOrientationMode.NorthUp,SonarEnabled=false,ShowCreatures=false,Movement=true,RangeM=300};
        var restored=JsonSerializer.Deserialize<UiPreferences>(JsonSerializer.Serialize(original))!;
        Assert.Equal(original,restored);
    }
}
