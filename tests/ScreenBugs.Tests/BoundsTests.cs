namespace ScreenBugs.Tests;

public sealed class BoundsTests
{
    private static readonly Bounds Screen = new(1920, 1080);

    [Fact]
    public void Contains_is_true_inside_and_on_the_edge_and_false_outside()
    {
        Assert.True(Screen.Contains(new Vector2(960, 540)));
        Assert.True(Screen.Contains(new Vector2(0, 0)));
        Assert.True(Screen.Contains(new Vector2(1920, 1080)));
        Assert.False(Screen.Contains(new Vector2(-1, 540)));
        Assert.False(Screen.Contains(new Vector2(960, 1081)));
    }

    [Fact]
    public void Clamp_pulls_points_inside_by_the_inset()
    {
        Assert.Equal(new Vector2(2, 2), Screen.Clamp(new Vector2(-50, -50), 2));
        Assert.Equal(new Vector2(1918, 1078), Screen.Clamp(new Vector2(5000, 5000), 2));
        Assert.Equal(new Vector2(960, 540), Screen.Clamp(new Vector2(960, 540), 2));
    }

    [Fact]
    public void Center_is_the_middle_of_the_screen()
    {
        Assert.Equal(new Vector2(960, 540), Screen.Center);
    }
}
