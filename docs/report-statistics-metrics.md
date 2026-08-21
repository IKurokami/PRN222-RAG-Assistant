# Report & Statistics metric dictionary

> Added on 2026-08-21 for the richer Flow 3 academic reporting dashboard.

## Goal

Flow 3 should answer operational and academic questions, not only display counters. Every metric must make clear:

1. what is measured;
2. what can reasonably be inferred from it;
3. what action it can support; and
4. what it **does not** prove.

All metrics are subject-scoped and read-only through:

```text
Pages/Reports/Index.cshtml.cs
 -> IReportQueryService
 -> ReportQueryService
 -> ApplicationDbContext
```

No new persistence schema is required. The richer report derives signals from existing `Subject`, `Chapter`, `Document`, `DocumentChunk`, `ChatSession`, `ChatMessage`, and `MessageCitation` data.

## Metric catalogue

| Metric | Formula / source | What it tells us | Practical action | Important limitation |
|---|---|---|---|---|
| Indexed readiness | `Indexed documents / all documents` | How much of the uploaded corpus is currently available to retrieval | Fix failed/backlogged documents before interpreting low retrieval coverage | Does not measure retrieval relevance |
| Average chunks per indexed document | `DocumentChunks / indexed documents` | Approximate corpus granularity and whether some documents are unusually fragmented | Review chunking/parser behavior when values are unexpectedly high/low | Chunk count alone does not measure chunk quality |
| User questions | Count of `ChatMessageRole.User` | Actual learner/query demand for the subject | Compare demand across time and against source/chapter usage | Does not identify question difficulty by itself |
| Average messages per session | `all subject messages / subject sessions` | Conversation depth / follow-up behavior | Detect whether usage is mostly one-shot or multi-turn | Longer sessions are not automatically better |
| Active sessions (7/30 days) | Distinct sessions with messages in the time window | Recent engagement trend | Detect drops/spikes in usage after content or UX changes | Demo/sample size may be small |
| Citation coverage | `assistant responses with >= 1 MessageCitation / assistant responses` | How often generated responses carry persisted evidence references | Investigate zero-citation responses, no-evidence handling, or citation persistence | **Not faithfulness**; a citation can still be irrelevant or unsupported |
| Average citations per assistant response | `assistant citations / assistant responses` | Evidence density in generated responses | Detect responses that cite too little or produce excessive citation noise | More citations are not automatically higher quality |
| Unique cited documents | Distinct documents reached through `MessageCitation -> DocumentChunk -> Document` | Breadth of sources actually used by RAG | Compare against indexed corpus to find unused content | Does not prove the cited source was necessary/correct |
| Cited source coverage | `distinct cited indexed documents / indexed documents` | How widely the indexed corpus is being exercised | Review retrieval, metadata, content overlap, or curriculum alignment when coverage is persistently narrow | Low coverage can be valid when user demand is narrow |
| Indexed but never cited | Indexed documents with zero observed citations | Potentially undiscoverable, irrelevant, redundant, or simply not-yet-needed sources | Inspect representative queries, metadata/chapter placement and chunking | Zero citations do not prove a document is bad |
| Top cited documents | Citation count grouped by document, plus distinct sessions and cited chunks | Which concrete sources repeatedly support answers | Prioritize source QA, freshness and maintenance for high-impact documents | Popularity is not correctness |
| Top cited chapters | Citation count grouped by the cited document's chapter | Which curriculum areas are most frequently retrieved | Compare curriculum demand with document supply; add/refresh sources for high-demand chapters | Citation frequency is a demand/retrieval proxy, not learning mastery |
| Top-3 source concentration | `citations from top 3 documents / all citations` | Whether RAG depends strongly on a very small source set | Check corpus duplication, ranking bias and missing coverage when concentration is unexpectedly high | High concentration may be correct for a narrow syllabus |
| 7-day activity series | Daily user messages, assistant messages and citations | Short-term demand and evidence-use trend | Correlate spikes/drops with content updates, demos or incidents | Not a long-term learning analytics model |
| Recent indexing failures | Latest failed documents and errors | Immediate corpus health issues | Repair parser/provider/file issues | Operational, not an academic quality score |

## Why these metrics are academically safer

RAG evaluation is multi-dimensional. The RAGAS work separates retrieval/context quality, faithfulness and answer relevance rather than treating one usage counter as overall RAG quality. The broader RAG evaluation literature likewise distinguishes retrieval and generation evaluation dimensions.

This report therefore uses database-observable metrics as **operational/usage proxies** and labels them accordingly. It does not rename citation coverage as faithfulness, or cited-document coverage as context recall.

For semantic quality evaluation, use the Evaluation workflow with suitable ground truth and/or judge-based metrics such as:

- context precision;
- context recall;
- faithfulness / groundedness;
- answer relevance/correctness;
- completeness/hallucination diagnostics where appropriate.

Those scores should only appear in Flow 3 after they are actually computed and persisted or made available through a trustworthy evaluation boundary.

## Interpretation examples

### Many indexed documents, low cited-source coverage

Possible explanations:

- learner questions currently cover only a narrow subset of the syllabus;
- retrieval ranking over-favors a few sources;
- duplicated documents compete with each other;
- metadata/chapter placement is weak;
- chunks are too broad/narrow or low quality.

Recommended follow-up: inspect top queries and retrieval results before deleting documents. Low coverage alone is not evidence that the unused documents should be removed.

### High citation coverage, low semantic Evaluation score

The system is attaching sources consistently, but those sources or the generated claims may be poor. Citation coverage should therefore be read as **evidence-use behavior**, not answer correctness.

### High top-3 concentration

The corpus may have one or two canonical sources that legitimately dominate. If that is unexpected, inspect source duplication, retrieval ranking, missing chapters and whether lower-ranked documents contain equivalent content.

### Chapter has few documents but many citations

This is a useful curriculum signal: demand/retrieval pressure is high relative to source supply. It can justify adding, refreshing or diversifying material for that chapter.

## References used for the metric design

- Es et al., **RAGAS: Automated Evaluation of Retrieval Augmented Generation**, arXiv:2309.15217 (2023).
- Yu et al., **Evaluation of Retrieval-Augmented Generation: A Survey**, arXiv:2405.07437 (2024).
- Recent RAG-for-report work also reports citation coverage/diversity as useful observable diagnostics, while keeping groundedness/citation correctness as separate quality dimensions.

## Security and scope

- Every aggregation remains constrained by `SubjectId`.
- The Report PageModel still requires `ManageDocuments` plus concrete `ISubjectAccessService` authorization.
- The report remains read-only and does not call embedding/chat providers.
- No cross-subject citation or chat totals are intentionally included.
