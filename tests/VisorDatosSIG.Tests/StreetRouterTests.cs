using VisorDatosSIG.Application;

namespace VisorDatosSIG.Tests;

public sealed class StreetRouterTests
{
    [Fact]
    public void Dijkstra_uses_street_segments_and_ignores_disconnected_destination()
    {
        var start = new MapPoint(-16.38, -60.96);
        var corner = new MapPoint(-16.38, -60.95);
        var first = new MapPoint(-16.37, -60.95);
        var second = new MapPoint(-16.36, -60.95);
        var island = new MapPoint(-16.379, -60.959);
        var router = new StreetRouter([[start, corner, first, second], [island, new MapPoint(-16.378, -60.959)]]);

        var result = router.Plan(start, [(1, second), (2, first), (3, island)], 3);

        Assert.Equal([2, 1], result.Stops);
        Assert.Equal([start, corner, first, second], result.Path);
        Assert.True(result.Meters > start.DistanceTo(second));
    }

    [Fact]
    public void Multiple_connections_can_share_the_same_street_node()
    {
        var start = new MapPoint(-16.38, -60.96);
        var end = new MapPoint(-16.38, -60.95);
        var router = new StreetRouter([[start, end]]);

        Assert.Equal([1, 2], router.Plan(start, [(2, start), (1, start)], 2).Stops);
        Assert.Throws<InvalidOperationException>(() => router.Plan(new MapPoint(0, 0), [], 1));
    }

    [Theory]
    [InlineData(15, true)]
    [InlineData(101, false)]
    [InlineData(0, false)]
    public void Field_gps_requires_acceptable_accuracy(double accuracy, bool valid)
    {
        Assert.Equal(valid, new VisorDatosSIG.Web.Controllers.OperacionesController.GpsInput(-16.38, -60.96, accuracy).IsValid);
    }
}
