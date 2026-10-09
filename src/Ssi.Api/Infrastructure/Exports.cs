using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using Ssi.Contracts;

namespace Ssi.Api.Infrastructure;

/// <summary>
/// Exports d'un <see cref="RapportDto"/> déjà calculé (R-40 : mêmes lignes et mêmes totaux que l'écran).
/// PDF : PDFsharp/MigraDoc (licence MIT, sans condition de chiffre d'affaires).
/// </summary>
public static class Exports
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-MA");

    private static string Texte(object? v, string type) => v switch
    {
        null => "",
        decimal d when type == "montant" => d.ToString("N2", Fr),
        decimal d => d.ToString("0.###", Fr),
        DateOnly d => d.ToString("dd/MM/yyyy", Fr),
        DateTimeOffset d => d.ToString("dd/MM/yyyy HH:mm", Fr),
        _ => v.ToString() ?? ""
    };

    /// <summary>CSV UTF-8 avec BOM, séparateur « ; » et virgule décimale (ouverture directe dans Excel en français).</summary>
    public static byte[] Csv(RapportDto r)
    {
        var sb = new StringBuilder();
        static string Echapper(string s) => s.Contains(';') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        sb.AppendLine(string.Join(';', r.Colonnes.Select(c => Echapper(c.Libelle))));
        foreach (var l in r.Lignes)
            sb.AppendLine(string.Join(';', r.Colonnes.Select(c => Echapper(l.TryGetValue(c.Cle, out var v) ? v switch
            {
                decimal d => d.ToString("0.######", Fr),
                _ => Texte(v, c.Type)
            } : ""))));
        if (r.Totaux.Count > 0)
            sb.AppendLine(string.Join(';', r.Colonnes.Select((c, i) => r.Totaux.TryGetValue(c.Cle, out var t) ? t.ToString("0.######", Fr) : i == 0 ? "TOTAL" : "")));
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    public static byte[] Excel(RapportDto r)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(r.Code.Length > 30 ? r.Code[..30] : r.Code);
        ws.Cell(1, 1).Value = r.Titre;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        ws.Cell(2, 1).Value = r.Filtres;
        ws.Cell(3, 1).Value = $"Généré le {r.GenereLe.ToLocalTime():dd/MM/yyyy HH:mm} par {r.GenerePar}";
        const int entete = 5;
        for (var i = 0; i < r.Colonnes.Count; i++)
        {
            ws.Cell(entete, i + 1).Value = r.Colonnes[i].Libelle;
            ws.Cell(entete, i + 1).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.LightGray);
        }
        var ligne = entete + 1;
        foreach (var l in r.Lignes)
        {
            for (var i = 0; i < r.Colonnes.Count; i++)
            {
                var c = r.Colonnes[i];
                var cell = ws.Cell(ligne, i + 1);
                switch (l.GetValueOrDefault(c.Cle))
                {
                    case decimal d: cell.Value = d; cell.Style.NumberFormat.Format = c.Type == "montant" ? "#,##0.00" : "#,##0.###"; break;
                    case DateOnly d: cell.Value = d.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = "dd/mm/yyyy"; break;
                    case null: break;
                    case var v: cell.Value = v.ToString(); break;
                }
            }
            ligne++;
        }
        if (r.Totaux.Count > 0)
        {
            ws.Cell(ligne, 1).Value = "TOTAL";
            for (var i = 0; i < r.Colonnes.Count; i++)
                if (r.Totaux.TryGetValue(r.Colonnes[i].Cle, out var t))
                {
                    ws.Cell(ligne, i + 1).Value = t;
                    ws.Cell(ligne, i + 1).Style.NumberFormat.Format = "#,##0.00";
                }
            ws.Row(ligne).Style.Font.SetBold();
        }
        ws.Columns().AdjustToContents(entete, ligne);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] Pdf(RapportDto r, string societe)
    {
        PolicesPdf.Initialiser();
        var doc = NouveauDocument(r.Titre);
        var section = NouvelleSection(doc, r.Colonnes.Count > 6 ? Orientation.Landscape : Orientation.Portrait, 1.2);
        section.Headers.Primary.AddParagraph($"{societe} — {r.Titre}").Format.Font.Size = 8;
        var pied = section.Footers.Primary.AddParagraph();
        pied.Format.Font.Size = 7;
        pied.AddText($"Généré le {r.GenereLe.ToLocalTime():dd/MM/yyyy HH:mm} par {r.GenerePar} — SSI, outil de gestion (n'est pas un logiciel de comptabilité certifié) — page ");
        pied.AddPageField();
        pied.AddText(" / ");
        pied.AddNumPagesField();

        var titre = section.AddParagraph(r.Titre);
        titre.Format.Font.Size = 14; titre.Format.Font.Bold = true;
        section.AddParagraph(r.Filtres).Format.SpaceAfter = Unit.FromPoint(8);

        var largeur = (section.PageSetup.Orientation == Orientation.Landscape ? 29.7 : 21.0) - 2.4;
        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.Format.Font.Size = r.Colonnes.Count > 8 ? 6.5 : 7.5;
        foreach (var c in r.Colonnes)
        {
            var col = table.AddColumn(Unit.FromCentimeter(largeur / r.Colonnes.Count));
            col.Format.Alignment = c.Type is "montant" or "quantite" ? ParagraphAlignment.Right : ParagraphAlignment.Left;
        }
        var h = table.AddRow();
        h.HeadingFormat = true; h.Format.Font.Bold = true; h.Shading.Color = Colors.LightGray;
        for (var i = 0; i < r.Colonnes.Count; i++) h.Cells[i].AddParagraph(r.Colonnes[i].Libelle);
        foreach (var l in r.Lignes)
        {
            var row = table.AddRow();
            for (var i = 0; i < r.Colonnes.Count; i++) row.Cells[i].AddParagraph(Texte(l.GetValueOrDefault(r.Colonnes[i].Cle), r.Colonnes[i].Type));
        }
        if (r.Totaux.Count > 0)
        {
            var row = table.AddRow();
            row.Format.Font.Bold = true;
            for (var i = 0; i < r.Colonnes.Count; i++)
                row.Cells[i].AddParagraph(r.Totaux.TryGetValue(r.Colonnes[i].Cle, out var t) ? Texte(t, r.Colonnes[i].Type) : i == 0 ? "TOTAL" : "");
        }
        return Rendre(doc);
    }

    public sealed record InfosSociete(string RaisonSociale, string? FormeJuridique, string? Ice, string? IdentifiantFiscal, string? RegistreCommerce, string? Patente,
        string? Cnss, string? Adresse, string? Ville, string? Telephone, string? Email, string? MentionsLegales);
    public sealed record InfosTiers(string Nom, string? Ice, string? Adresse, string? Ville);

    /// <summary>Facture ou avoir client au format PDF, avec les identifiants légaux de l'émetteur et du client.</summary>
    public static byte[] Facture(FactureDto f, InfosSociete s, InfosTiers? client)
    {
        PolicesPdf.Initialiser();
        var avoir = f.Type == TypeFacture.Avoir;
        var doc = NouveauDocument($"{(avoir ? "Avoir" : "Facture")} {f.Numero}");
        var sec = NouvelleSection(doc, Orientation.Portrait, 1.8);

        var e = sec.AddParagraph(s.RaisonSociale + (s.FormeJuridique is null ? "" : $" ({s.FormeJuridique})"));
        e.Format.Font.Size = 13; e.Format.Font.Bold = true;
        sec.AddParagraph(string.Join(" — ", new[] { s.Adresse, s.Ville, s.Telephone, s.Email }.Where(x => !string.IsNullOrWhiteSpace(x)))).Format.Font.Size = 8;

        var t = sec.AddParagraph($"{(avoir ? "AVOIR" : "FACTURE")} N° {f.Numero}");
        t.Format.Font.Size = 15; t.Format.Font.Bold = true; t.Format.SpaceBefore = Unit.FromPoint(14);
        var infos = sec.AddParagraph($"Date : {f.Date:dd/MM/yyyy}" + (avoir ? "" : $"    Échéance : {f.Echeance:dd/MM/yyyy}"));
        infos.Format.SpaceAfter = Unit.FromPoint(6);

        var c = sec.AddParagraph();
        c.Format.Borders.Width = 0.5; c.Format.Borders.Distance = 4; c.Format.LeftIndent = Unit.FromCentimeter(9);
        c.AddFormattedText("Client : ", TextFormat.Bold); c.AddText(client?.Nom ?? f.Tiers);
        if (!string.IsNullOrWhiteSpace(client?.Adresse)) { c.AddLineBreak(); c.AddText($"{client!.Adresse} {client.Ville}"); }
        if (!string.IsNullOrWhiteSpace(client?.Ice)) { c.AddLineBreak(); c.AddText($"ICE : {client!.Ice}"); }
        c.Format.SpaceAfter = Unit.FromPoint(12);

        var table = sec.AddTable();
        table.Borders.Width = 0.25;
        table.Format.Font.Size = 8.5;
        foreach (var (w, a) in new[] { (7.2, ParagraphAlignment.Left), (2.0, ParagraphAlignment.Right), (2.3, ParagraphAlignment.Right), (1.4, ParagraphAlignment.Right), (2.6, ParagraphAlignment.Right) })
            table.AddColumn(Unit.FromCentimeter(w)).Format.Alignment = a;
        var h = table.AddRow();
        h.Format.Font.Bold = true; h.Shading.Color = Colors.LightGray; h.HeadingFormat = true;
        string[] entetes = ["Désignation", "Quantité", "PU HT", "TVA", "Montant HT"];
        for (var i = 0; i < entetes.Length; i++) h.Cells[i].AddParagraph(entetes[i]);
        foreach (var l in f.Lignes)
        {
            var row = table.AddRow();
            row.Cells[0].AddParagraph(l.Designation);
            row.Cells[1].AddParagraph(l.Quantite.ToString("N3", Fr));
            row.Cells[2].AddParagraph(l.PrixUnitaireHt.ToString("N4", Fr));
            row.Cells[3].AddParagraph($"{l.TauxTva * 100:0.##} %");
            row.Cells[4].AddParagraph(l.MontantHt.ToString("N2", Fr));
        }

        var tot = sec.AddTable();
        tot.Format.Font.Size = 9;
        tot.Rows.LeftIndent = Unit.FromCentimeter(9.5);
        tot.AddColumn(Unit.FromCentimeter(3.4));
        tot.AddColumn(Unit.FromCentimeter(2.6)).Format.Alignment = ParagraphAlignment.Right;
        void Total(string lib, decimal v, bool gras = false)
        {
            var row = tot.AddRow();
            row.Format.Font.Bold = gras;
            row.Cells[0].AddParagraph(lib);
            row.Cells[1].AddParagraph(v.ToString("N2", Fr));
        }
        tot.AddRow();
        Total("Total HT", f.TotalHt);
        foreach (var g in f.Lignes.GroupBy(l => l.TauxTva).OrderBy(g => g.Key)) Total($"TVA {g.Key * 100:0.##} %", g.Sum(l => l.MontantTva));
        Total("Total TTC (DH)", f.TotalTtc, true);

        if (!string.IsNullOrWhiteSpace(f.Commentaire)) sec.AddParagraph(f.Commentaire).Format.SpaceBefore = Unit.FromPoint(10);
        if (f.Origine == OrigineFacture.BonsStation)
            sec.AddParagraph("Facture récapitulative des bons de carburant servis à la station.").Format.Font.Italic = true;

        var pied = sec.Footers.Primary.AddParagraph();
        pied.Format.Font.Size = 7; pied.Format.Alignment = ParagraphAlignment.Center;
        pied.AddText(string.Join(" — ", new[]
        {
            s.Ice is null ? null : $"ICE {s.Ice}", s.IdentifiantFiscal is null ? null : $"IF {s.IdentifiantFiscal}", s.RegistreCommerce is null ? null : $"RC {s.RegistreCommerce}",
            s.Patente is null ? null : $"Patente {s.Patente}", s.Cnss is null ? null : $"CNSS {s.Cnss}"
        }.Where(x => x is not null)));
        if (!string.IsNullOrWhiteSpace(s.MentionsLegales)) { pied.AddLineBreak(); pied.AddText(s.MentionsLegales); }
        return Rendre(doc);
    }

    private static Document NouveauDocument(string titre)
    {
        var doc = new Document();
        doc.Info.Title = titre;
        doc.Info.Author = "SSI";
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = PolicesPdf.Famille;
        normal.Font.Size = 9;
        return doc;
    }

    private static Section NouvelleSection(Document doc, Orientation orientation, double margeCm)
    {
        var section = doc.AddSection();
        section.PageSetup = doc.DefaultPageSetup.Clone();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = orientation;
        section.PageSetup.TopMargin = section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.LeftMargin = section.PageSetup.RightMargin = Unit.FromCentimeter(margeCm);
        return section;
    }

    private static byte[] Rendre(Document doc)
    {
        var r = new PdfDocumentRenderer { Document = doc };
        r.RenderDocument();
        using var ms = new MemoryStream();
        r.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }
}

/// <summary>Polices du PDF : Arial sous Windows (serveur de production), Liberation/DejaVu sous Linux (tests).</summary>
public sealed class PolicesPdf : IFontResolver
{
    public const string Famille = "SsiSans";
    private static readonly object Verrou = new();
    private static bool initialise;
    private readonly string normal, gras;

    private PolicesPdf(string normal, string gras) { this.normal = normal; this.gras = gras; }

    public static void Initialiser()
    {
        lock (Verrou)
        {
            if (initialise) return;
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            (string, string)[] candidats =
            [
                (Path.Combine(windows, "arial.ttf"), Path.Combine(windows, "arialbd.ttf")),
                ("/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"),
                ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
            ];
            var (n, g) = candidats.FirstOrDefault(c => File.Exists(c.Item1) && File.Exists(c.Item2));
            if (n is null) throw new InvalidOperationException("Aucune police TrueType disponible pour générer les PDF (Arial, Liberation Sans ou DejaVu Sans).");
            GlobalFontSettings.FontResolver = new PolicesPdf(n, g);
            initialise = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) => new(bold ? "ssi-gras" : "ssi-normal");
    public byte[]? GetFont(string faceName) => File.ReadAllBytes(faceName == "ssi-gras" ? gras : normal);
}
