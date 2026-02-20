namespace ContentUnderstanding.Client;

/// <summary>
/// Provides constants for all available prebuilt analyzer IDs in Azure AI Content Understanding.
/// Use these with <see cref="ContentUnderstandingClient"/> methods to analyze content
/// without creating a custom analyzer.
/// </summary>
public static class PrebuiltAnalyzers
{
    // ── Content Extraction ──────────────────────────────────────────────

    /// <summary>
    /// Basic OCR: extracts words, paragraphs, formulas, and barcodes from documents.
    /// </summary>
    public const string Read = "prebuilt-read";

    /// <summary>
    /// Extracts content with layout and structure understanding, including tables,
    /// figures, sections, annotations, and hyperlinks.
    /// </summary>
    public const string Layout = "prebuilt-layout";

    // ── Base Modality Analyzers ─────────────────────────────────────────

    /// <summary>
    /// Core analyzer for document content (text and tables).
    /// Can be extended for custom document scenarios.
    /// </summary>
    public const string Document = "prebuilt-document";

    /// <summary>
    /// Core analyzer for image content.
    /// Can be extended for custom image scenarios.
    /// </summary>
    public const string Image = "prebuilt-image";

    /// <summary>
    /// Core analyzer for audio content (transcription and audio analysis).
    /// Can be extended for custom audio scenarios.
    /// </summary>
    public const string Audio = "prebuilt-audio";

    /// <summary>
    /// Core analyzer for video content (transcripts and metadata extraction).
    /// Can be extended for custom video scenarios.
    /// </summary>
    public const string Video = "prebuilt-video";

    // ── Search / RAG Analyzers ──────────────────────────────────────────

    /// <summary>
    /// Semantic extraction for document search and knowledge-base ingestion.
    /// </summary>
    public const string DocumentSearch = "prebuilt-documentSearch";

    /// <summary>
    /// Visual content descriptions and key image features for search.
    /// </summary>
    public const string ImageSearch = "prebuilt-imageSearch";

    /// <summary>
    /// Conversation transcription, speaker diarization, and multilingual support for search.
    /// </summary>
    public const string AudioSearch = "prebuilt-audioSearch";

    /// <summary>
    /// Automated transcript and segment-based extraction, scene detection for search.
    /// </summary>
    public const string VideoSearch = "prebuilt-videoSearch";

    // ── Domain-Specific Analyzers ───────────────────────────────────────

    /// <summary>
    /// Extracts invoice data (IDs, totals, parties, line items, etc.).
    /// </summary>
    public const string Invoice = "prebuilt-invoice";

    /// <summary>
    /// Extracts fields from receipts (merchant, items, totals).
    /// </summary>
    public const string Receipt = "prebuilt-receipt";

    /// <summary>
    /// Extracts structured data from identity documents (name, birthdate, ID number).
    /// </summary>
    public const string IdDocument = "prebuilt-idDocument";
}
