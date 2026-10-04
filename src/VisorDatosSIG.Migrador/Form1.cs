namespace VisorDatosSIG.Migrador;

public partial class Form1 : Form
{
    private readonly TextBox _pathBox = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Label _summary = new() { AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly Label _components = new() { AutoSize = true };
    private readonly Label _details = new() { AutoSize = true };
    private readonly Label _prj = new() { AutoSize = true };
    private readonly DataGridView _fields = CreateGrid();
    private readonly DataGridView _preview = CreateGrid();
    private readonly VisorDatosSIG.Infrastructure.Shapefiles.IShapefileDiagnosticService _diagnostics = new VisorDatosSIG.Infrastructure.Shapefiles.ShapefileDiagnosticService();

    public Form1()
    {
        InitializeComponent();
        BuildInterface();
    }

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.White,
        BorderStyle = BorderStyle.None
    };

    private void BuildInterface()
    {
        Text = "VisorDatosSIG | Diagnóstico de Shapefiles";
        ClientSize = new Size(1100, 720);
        MinimumSize = new Size(900, 600);
        BackColor = Color.FromArgb(244, 247, 250);

        var selectFolder = new Button { Text = "Seleccionar carpeta", AutoSize = true };
        selectFolder.Click += (_, _) => SelectFolder();
        var selectShp = new Button { Text = "Seleccionar SHP", AutoSize = true };
        selectShp.Click += (_, _) => SelectShapefile();
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(12, 10, 12, 4), BackColor = Color.FromArgb(20, 54, 82) };
        toolbar.Controls.AddRange([selectFolder, selectShp, _pathBox]);

        var overview = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 120, Padding = new Padding(18, 16, 18, 8), FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White };
        overview.Controls.AddRange([_summary, _components, _details, _prj]);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(new TabPage("Campos DBF") { Controls = { _fields } });
        tabs.TabPages.Add(new TabPage("Primeros 20 registros") { Controls = { _preview } });
        Controls.Add(tabs);
        Controls.Add(overview);
        Controls.Add(toolbar);
    }

    private void SelectFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Seleccione una carpeta que contenga un Shapefile" };
        if (dialog.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var shapefile = Directory.EnumerateFiles(dialog.SelectedPath, "*.shp").FirstOrDefault();
        if (shapefile is null)
        {
            MessageBox.Show("La carpeta seleccionada no contiene archivos .shp.", "Diagnóstico", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        LoadShapefile(shapefile);
    }

    private void SelectShapefile()
    {
        using var dialog = new OpenFileDialog { Filter = "Shapefile (*.shp)|*.shp", Title = "Seleccione un Shapefile" };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            LoadShapefile(dialog.FileName);
        }
    }

    private async void LoadShapefile(string path)
    {
        try
        {
            UseWaitCursor = true;
            _pathBox.Text = path;
            var result = await Task.Run(() => (_diagnostics.GetDiagnostics(path), _diagnostics.ReadFeaturesFromPath(path)));
            var diagnostics = result.Item1;
            _summary.Text = $"{diagnostics.Name} | {diagnostics.RecordCount:N0} registros | {diagnostics.GeometryType}";
            _components.Text = $"Componentes: SHP {(diagnostics.HasShp ? "OK" : "Falta")}  SHX {(diagnostics.HasShx ? "OK" : "Falta")}  DBF {(diagnostics.HasDbf ? "OK" : "Falta")}  PRJ {(diagnostics.HasPrj ? "OK" : "Falta")}";
            _details.Text = diagnostics.BoundingBox is null ? "Extensión: no disponible" : $"Extensión: X {diagnostics.BoundingBox.MinX:F6} a {diagnostics.BoundingBox.MaxX:F6} | Y {diagnostics.BoundingBox.MinY:F6} a {diagnostics.BoundingBox.MaxY:F6}";
            _prj.Text = diagnostics.PrjStatus;
            _fields.DataSource = diagnostics.Fields.ToList();
            BindPreview(result.Item2);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "No fue posible leer el Shapefile", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void BindPreview(IReadOnlyList<VisorDatosSIG.Infrastructure.Shapefiles.ShapefileFeature> features)
    {
        _preview.Columns.Clear();
        var fields = features.SelectMany(feature => feature.Attributes.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var field in fields)
        {
            _preview.Columns.Add(field, field);
        }
        foreach (var feature in features)
        {
            _preview.Rows.Add(fields.Select(field => feature.Attributes.TryGetValue(field, out var value) ? value ?? string.Empty : string.Empty).ToArray());
        }
    }
}
