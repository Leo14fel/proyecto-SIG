using Microsoft.AspNetCore.Mvc;
using NetTopologySuite.Geometries;
using System.Text.Json;
using VisorDatosSIG.Infrastructure.Persistence;
using VisorDatosSIG.Web.Controllers;

namespace VisorDatosSIG.Tests;

public sealed class RegistrosApiControllerTests
{
    [Fact]
    public async Task Browse_rejects_unknown_layer_before_querying_service()
    {
        var service = new FakeLayers();
        var result = await new RegistrosApiController(service).Browse("tabla-libre", null, null, null, 1, 25);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, service.BrowseCalls);
    }

    [Fact]
    public async Task Browse_rejects_field_filter_without_search_value()
    {
        var result = await new RegistrosApiController(new FakeLayers()).Browse("manzanas", null, "UV", "igual", 1, 25);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Browse_uses_server_pagination_for_official_layer()
    {
        var service = new FakeLayers();
        var result = await new RegistrosApiController(service).Browse("lotes", "12", "NroLote", "igual", 2, 25);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.BrowseCalls);
        Assert.Equal(("lotes", "12", 2, 25, "NroLote", "igual"), service.LastBrowse);
    }

    [Fact]
    public async Task Browse_all_layers_fills_pages_across_layer_boundaries()
    {
        var service = new FakeLayers(new Dictionary<string, int> { ["manzanas"] = 3, ["lotes"] = 7, ["codigos-fijos"] = 1, ["vias"] = 0 });
        var controller = new RegistrosApiController(service);
        var result = Assert.IsType<OkObjectResult>(await controller.Browse("todas", null, null, null, 2, 5));
        var json = JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(11, json.GetProperty("total").GetInt32());
        Assert.Equal([3, 4, 5, 6, 7], json.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("Id").GetInt32()));

        var lastPage = Assert.IsType<OkObjectResult>(await controller.Browse("todas", null, null, null, 3, 5));
        var remaining = JsonSerializer.SerializeToElement(lastPage.Value).GetProperty("items").EnumerateArray();
        Assert.Equal("codigos-fijos", Assert.Single(remaining).GetProperty("LayerKey").GetString());
    }

    [Fact]
    public async Task Get_returns_not_found_for_missing_official_record()
    {
        var result = await new RegistrosApiController(new FakeLayers()).Get("lotes", 999999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private sealed class FakeLayers(IReadOnlyDictionary<string, int>? counts = null) : ISqlLayerService
    {
        public int BrowseCalls { get; private set; }
        public (string, string?, int, int, string?, string?) LastBrowse { get; private set; }
        public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<IReadOnlyList<SqlLayerInfo>> GetLayersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SqlLayerInfo>>([]);
        public Task<IReadOnlyList<SqlLayerFeature>> GetFeaturesAsync(string layerKey, Envelope? bbox, int limit, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SqlLayerFeature>>([]);
        public Task<SqlLayerFeature?> GetFeatureAsync(string layerKey, int id, CancellationToken cancellationToken = default) => Task.FromResult<SqlLayerFeature?>(null);
        public Task<IReadOnlyList<SqlLayerFeature>> GetConnectionsByStateAsync(Envelope? bbox, int limit, byte state, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SqlLayerFeature>>([]);
        public Task<SqlSearchPage> SearchAsync(string layerKey, string query, int page, int pageSize, string? field = null, string? comparison = null, CancellationToken cancellationToken = default) => BrowseAsync(layerKey, query, page, pageSize, field, comparison, cancellationToken);
        public Task<SqlSearchPage> BrowseAsync(string layerKey, string? query, int page, int pageSize, string? field = null, string? comparison = null, CancellationToken cancellationToken = default)
        {
            BrowseCalls++;
            LastBrowse = (layerKey, query, page, pageSize, field, comparison);
            var count = counts?.GetValueOrDefault(layerKey) ?? 1;
            var items = Enumerable.Range(1, count).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(id => new SqlSearchResult(layerKey, id, new Dictionary<string, object?> { ["Id"] = id })).ToList();
            return Task.FromResult(new SqlSearchPage(count, items));
        }
    }
}
