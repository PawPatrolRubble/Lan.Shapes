#nullable enable

using System;
using netDxf;

namespace Lan.Shapes.DialogGeometry
{
    /// <summary>
    /// Owns DXF document persistence so shape interaction code does not call the
    /// netDxf file APIs directly.  A host can replace this boundary in tests or
    /// when it needs custom loading/saving policies.
    /// </summary>
    public interface IDxfDocumentService
    {
        DxfDocument Load(string filePath);

        void Save(DxfDocument document, string filePath);
    }

    public sealed class DxfDocumentService : IDxfDocumentService
    {
        public static DxfDocumentService Default { get; } = new DxfDocumentService();

        public DxfDocument Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A DXF file path is required.", nameof(filePath));
            }

            return DxfDocument.Load(filePath);
        }

        public void Save(DxfDocument document, string filePath)
        {
            ArgumentNullException.ThrowIfNull(document);

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A DXF file path is required.", nameof(filePath));
            }

            document.Save(filePath);
        }
    }
}
