namespace VisorDatosSIG.Web.Models;

public sealed record GeographicRecordDetailViewModel(
    string LayerKey,
    string LayerName,
    int Id,
    IReadOnlyDictionary<string, object?> Properties,
    string ReturnUrl);
