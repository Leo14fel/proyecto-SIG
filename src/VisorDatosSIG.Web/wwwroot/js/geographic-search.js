(() => {
    const layers = {
        todas: { label: 'Todas las capas', hint: 'Ej.: apellido del cliente, nombre de una calle, lote 12 o manzana 7', fields: {} },
        'codigos-fijos': { label: 'Conexiones de agua', hint: 'Ej.: apellido del cliente o código fijo', fields: { Nombre: 'Nombre del cliente', CodFijo: 'Código fijo', CodF_SIG: 'Código SIG' } },
        vias: { label: 'Vías', hint: 'Ej.: nombre de una calle, avenida o tipo de vía', fields: { Nombre: 'Nombre de la vía', TipoVia: 'Tipo de vía' } },
        manzanas: { label: 'Manzanas', hint: 'Ej.: manzana 7, UV 12 o UV 12 manzana 7', fields: { MZA: 'Número de manzana', UV: 'Unidad vecinal (UV)', UV_MZA: 'Código de manzana' } },
        lotes: { label: 'Lotes', hint: 'Ej.: lote 12. Un mismo número puede aparecer en varios lugares.', fields: { NroLote: 'Número de lote' } }
    };
    const states = ['Normal', 'Para corte', 'Cortado', 'Baja parcial', 'Baja total'];
    const value = input => input == null ? '' : String(input).trim();
    const entry = (label, text) => [label, value(text)];

    function describe(layer, data = {}) {
        const p = data ?? {};
        let title;
        let details;
        switch (layer) {
            case 'codigos-fijos':
                title = value(p.Nombre) || (value(p.CodFijo) ? 'Conexión ' + p.CodFijo : 'Conexión sin nombre');
                details = [entry('Código fijo', p.CodFijo), entry('Código SIG', p.CodF_SIG), entry('Estado', states[Number(p.Estado) - 1])];
                break;
            case 'vias':
                title = value(p.Nombre) || 'Vía sin nombre registrado';
                details = [entry('Tipo de vía', p.TipoVia)];
                break;
            case 'manzanas':
                title = value(p.MZA) ? 'Manzana ' + p.MZA : (value(p.UV_MZA) ? 'Manzana ' + p.UV_MZA : 'Manzana sin código registrado');
                details = [entry('Unidad vecinal', p.UV), entry('Código de manzana', p.UV_MZA)];
                break;
            case 'lotes':
                title = value(p.NroLote) ? 'Lote ' + p.NroLote : 'Lote sin número registrado';
                details = [];
                break;
            default:
                title = 'Registro geográfico';
                details = [];
        }
        return { title, details: details.filter(([, text]) => text), layer: layers[layer]?.label || 'Registro geográfico' };
    }

    function setup(layer, input, field, comparison, selectedField = '', selectedOperator = 'contiene') {
        const config = layers[layer] || layers.todas;
        const allowed = { ...config.fields };
        if (layer === 'vias' && selectedField === 'OSMID') allowed.OSMID = 'Referencia OpenStreetMap';
        field.replaceChildren(new Option('Nombre o referencia', ''), ...Object.entries(allowed).map(([key, name]) => new Option(name, key)));
        field.value = Object.hasOwn(allowed, selectedField) ? selectedField : '';
        comparison.value = ['contiene', 'igual', 'comienza-con'].includes(selectedOperator) ? selectedOperator : 'contiene';
        input.placeholder = config.hint;
        updateOperator(field, comparison);
    }

    function updateOperator(field, comparison) {
        const numeric = field.value === 'CodFijo';
        comparison.disabled = !field.value;
        for (const option of comparison.options) {
            option.hidden = option.disabled = numeric && option.value !== 'igual';
        }
        if (numeric) comparison.value = 'igual';
    }

    window.geographicSearch = { layers, describe, setup, updateOperator };
})();
