# ============================================================================
# DockAi Meta Ontology — Self-Documenting + Forensic Analysis Example
# ============================================================================
# This file demonstrates DockAi describing itself AND modeling a real-world
# forensic document analysis case (Erard vs Qwant).
#
# Three layers:
#   1. Architecture  — what DockAi is built from
#   2. Domain        — what DockAi thinks about (self-referential)
#   3. Forensic      — real Qwant affair entities, relations, events
# ============================================================================


# ============================================================================
# PART 1: ENTITY TYPE DEFINITIONS
# ============================================================================

# --- Architecture types ---

@type solution {
    name: string
    path: string
    description: string
    aliases: string[]
}

@type project {
    name: string
    port: number
    framework: string
    role: enum [api, viewer, shared]
    aliases: string[]
}

@type module {
    name: string
    responsibility: string
    namespace: string
    aliases: string[]
}

@type component {
    name: string
    file_path: string
    purpose: string
    aliases: string[]
}

@type library {
    name: string
    version: string
    purpose: string
    aliases: string[]
}

@type file_format {
    name: string
    extensions: string[]
    mime_type: string
    aliases: string[]
}

@type endpoint {
    name: string
    method: enum [GET, POST, PUT, DELETE]
    path: string
    description: string
    aliases: string[]
}

# --- Domain concept types ---

@type concept {
    name: string
    description: string
    dsl_keyword: string
    aliases: string[]
}

@type capability {
    name: string
    description: string
    input: string
    output: string
    aliases: string[]
}

# --- Forensic / Case types ---

@type person {
    name: string
    role: enum [plaignant, developpeur, avocat, dpo, dirigeant, journaliste, temoin, harceleur]
    organization: string
    aliases: string[]
}

@type organization {
    name: string
    type: enum [entreprise, autorite, juridiction, media, plateforme, investisseur]
    country: string
    aliases: string[]
}

@type event {
    name: string
    date: string
    event_type: enum [technique, alerte, licenciement, decision, harcelement, juridique, audit, publication]
    description: string
    aliases: string[]
}

@type legal_reference {
    name: string
    code: enum [code_penal, code_travail, rgpd, directive_ue, jurisprudence]
    article: string
    description: string
    aliases: string[]
}

@type evidence {
    name: string
    evidence_type: enum [commit, email, sms, document, tweet, capture, audit, temoignage]
    date: string
    source: string
    description: string
    aliases: string[]
}

@type jira_ticket {
    name: string
    key: string
    commit_count: number
    description: string
    aliases: string[]
}

@type financial_claim {
    name: string
    category: enum [salaire, prejudice_moral, perte_chance, dommages_interets, frais]
    description: string
    aliases: string[]
}


# ============================================================================
# PART 2: ENTITY INSTANCES — Architecture Layer
# ============================================================================

# --- Solution ---

@entity dockai_sln : solution {
    name: "DockAi"
    path: "DockAi.sln"
    description: "Document intelligence engine with DSL-driven ontology"
    aliases: ["dockai", "dockaisln", "dock ai", "dockaiproject"]
}

# --- Projects ---

@entity dockai_api : project {
    name: "DockAi.Api"
    port: 5108
    framework: "net9.0"
    role: api
    aliases: ["dockaiapi", "api", "backend", "dockai api"]
}

@entity dockai_viewer : project {
    name: "DockAi.Viewer"
    port: 5109
    framework: "net9.0"
    role: viewer
    aliases: ["dockaiviewer", "viewer", "frontend", "dockai viewer"]
}

# --- Modules ---

@entity mod_dsl : module {
    name: "DSL"
    responsibility: "Lexing, parsing, and validating the ontology definition language"
    namespace: "DockAi.Api.Dsl"
    aliases: ["dsl", "dsl module", "ontology language", "dsl parser"]
}

@entity mod_parsing : module {
    name: "Parsing"
    responsibility: "Converting document formats (docx, pdf, xlsx...) to searchable text"
    namespace: "DockAi.Api.Parsing"
    aliases: ["parsing", "document parsing", "parsers", "format conversion"]
}

@entity mod_indexing : module {
    name: "Indexing"
    responsibility: "Building and maintaining Lucene.NET full-text search index"
    namespace: "DockAi.Api.Indexing"
    aliases: ["indexing", "lucene", "index", "full text search", "lucene index"]
}

@entity mod_analysis : module {
    name: "Analysis"
    responsibility: "Entity extraction, rule classification, and link discovery"
    namespace: "DockAi.Api.Analysis"
    aliases: ["analysis", "extraction", "classification", "link building"]
}

@entity mod_search : module {
    name: "Search"
    responsibility: "DSL query parsing and Lucene search execution"
    namespace: "DockAi.Api.Search"
    aliases: ["search", "query", "search engine", "dsl query"]
}

@entity mod_ontology : module {
    name: "Ontology"
    responsibility: "Managing entity types, taxonomy trees, and ontology state"
    namespace: "DockAi.Api.Ontology"
    aliases: ["ontology", "ontology manager", "taxonomy store", "type store"]
}

@entity mod_rendering : module {
    name: "Rendering"
    responsibility: "Server-side conversion of documents to HTML for browser display"
    namespace: "DockAi.Viewer.Rendering"
    aliases: ["rendering", "renderers", "html conversion", "document rendering"]
}

@entity mod_frontend : module {
    name: "Frontend"
    responsibility: "Browser-side views: sidebar, document view, graph, timeline, search"
    namespace: "DockAi.Viewer.wwwroot"
    aliases: ["frontend", "ui", "browser", "javascript", "views"]
}

# --- Key Components ---

@entity dsl_lexer : component {
    name: "DslLexer"
    file_path: "src/DockAi.Api/Dsl/DslLexer.cs"
    purpose: "Tokenizes DSL source into typed tokens (identifiers, strings, keywords)"
    aliases: ["dsllexer", "lexer", "tokenizer", "dsl tokenizer"]
}

@entity dsl_schema_parser : component {
    name: "DslSchemaParser"
    file_path: "src/DockAi.Api/Dsl/DslSchemaParser.cs"
    purpose: "Parses @type, @entity, @relation, @taxonomy, @rule constructs"
    aliases: ["dslschemaparser", "schema parser", "ontology parser"]
}

@entity dsl_query_parser : component {
    name: "DslQueryParser"
    file_path: "src/DockAi.Api/Search/DslQueryParser.cs"
    purpose: "Translates DSL search queries into Lucene Query objects"
    aliases: ["dslqueryparser", "query parser", "search parser"]
}

@entity parser_factory : component {
    name: "ParserFactory"
    file_path: "src/DockAi.Api/Parsing/ParserFactory.cs"
    purpose: "Selects the correct IDocumentParser based on file extension"
    aliases: ["parserfactory", "parser factory", "format selector"]
}

@entity docx_parser : component {
    name: "DocxParser"
    file_path: "src/DockAi.Api/Parsing/DocxParser.cs"
    purpose: "Extracts text from .docx using DocumentFormat.OpenXml"
    aliases: ["docxparser", "word parser", "docx extractor"]
}

@entity xlsx_parser : component {
    name: "XlsxParser"
    file_path: "src/DockAi.Api/Parsing/XlsxParser.cs"
    purpose: "Extracts text from .xlsx using ClosedXML"
    aliases: ["xlsxparser", "excel parser", "xlsx extractor"]
}

@entity csv_parser : component {
    name: "CsvParser"
    file_path: "src/DockAi.Api/Parsing/CsvParser.cs"
    purpose: "Extracts text from .csv using CsvHelper"
    aliases: ["csvparser", "csv extractor"]
}

@entity pdf_parser : component {
    name: "PdfParser"
    file_path: "src/DockAi.Api/Parsing/PdfParser.cs"
    purpose: "Extracts text from .pdf using PdfPig"
    aliases: ["pdfparser", "pdf extractor"]
}

@entity md_parser : component {
    name: "MarkdownParser"
    file_path: "src/DockAi.Api/Parsing/MarkdownParser.cs"
    purpose: "Converts Markdown to plain text using Markdig"
    aliases: ["markdownparser", "md parser"]
}

@entity html_parser : component {
    name: "HtmlParser"
    file_path: "src/DockAi.Api/Parsing/HtmlParser.cs"
    purpose: "Extracts text from HTML using HtmlAgilityPack"
    aliases: ["htmlparser", "html extractor"]
}

@entity txt_parser : component {
    name: "TxtParser"
    file_path: "src/DockAi.Api/Parsing/TxtParser.cs"
    purpose: "Reads plain text files directly"
    aliases: ["txtparser", "text parser"]
}

@entity lucene_indexer : component {
    name: "LuceneIndexer"
    file_path: "src/DockAi.Api/Indexing/LuceneIndexer.cs"
    purpose: "Writes documents to Lucene.NET index with fields and facets"
    aliases: ["luceneindexer", "indexer", "index writer"]
}

@entity index_manager : component {
    name: "IndexManager"
    file_path: "src/DockAi.Api/Indexing/IndexManager.cs"
    purpose: "Orchestrates full pipeline: parse, extract, classify, link, index"
    aliases: ["indexmanager", "index manager", "pipeline orchestrator"]
}

@entity entity_extractor : component {
    name: "EntityExtractor"
    file_path: "src/DockAi.Api/Analysis/EntityExtractor.cs"
    purpose: "Finds entity mentions in text via regex alias matching"
    aliases: ["entityextractor", "entity extractor", "ner", "mention finder"]
}

@entity rule_engine : component {
    name: "RuleEngine"
    file_path: "src/DockAi.Api/Analysis/RuleEngine.cs"
    purpose: "Applies @rule patterns to auto-classify documents into taxonomy nodes"
    aliases: ["ruleengine", "rule engine", "classifier", "auto classifier"]
}

@entity link_builder : component {
    name: "LinkBuilder"
    file_path: "src/DockAi.Api/Analysis/LinkBuilder.cs"
    purpose: "Discovers document connections via Jaccard similarity of shared entities"
    aliases: ["linkbuilder", "link builder", "jaccard", "link discovery"]
}

@entity search_service : component {
    name: "SearchService"
    file_path: "src/DockAi.Api/Search/SearchService.cs"
    purpose: "Executes Lucene queries with faceted filtering and result ranking"
    aliases: ["searchservice", "search service", "search executor"]
}

@entity ontology_manager : component {
    name: "OntologyManager"
    file_path: "src/DockAi.Api/Ontology/OntologyManager.cs"
    purpose: "Loads DSL files, manages entity/relation/taxonomy stores"
    aliases: ["ontologymanager", "ontology manager", "store manager"]
}

@entity renderer_factory : component {
    name: "RendererFactory"
    file_path: "src/DockAi.Viewer/Rendering/RendererFactory.cs"
    purpose: "Selects the correct IDocumentRenderer based on file type"
    aliases: ["rendererfactory", "renderer factory"]
}

@entity api_client_js : component {
    name: "ApiClient"
    file_path: "src/DockAi.Viewer/wwwroot/js/api-client.js"
    purpose: "Browser-side HTTP client for all API calls via /proxy"
    aliases: ["apiclient", "api client", "javascript client"]
}

# --- File Formats ---

@entity fmt_docx : file_format {
    name: "Word Document"
    extensions: [".docx"]
    mime_type: "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    aliases: ["docx", "word", ".docx"]
}

@entity fmt_xlsx : file_format {
    name: "Excel Spreadsheet"
    extensions: [".xlsx"]
    mime_type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    aliases: ["xlsx", "excel", ".xlsx"]
}

@entity fmt_csv : file_format {
    name: "CSV"
    extensions: [".csv"]
    mime_type: "text/csv"
    aliases: ["csv", ".csv"]
}

@entity fmt_pdf : file_format {
    name: "PDF"
    extensions: [".pdf"]
    mime_type: "application/pdf"
    aliases: ["pdf", ".pdf"]
}

@entity fmt_md : file_format {
    name: "Markdown"
    extensions: [".md"]
    mime_type: "text/markdown"
    aliases: ["markdown", "md", ".md"]
}

@entity fmt_html : file_format {
    name: "HTML"
    extensions: [".html", ".htm"]
    mime_type: "text/html"
    aliases: ["html", ".html", "htm"]
}

@entity fmt_txt : file_format {
    name: "Plain Text"
    extensions: [".txt"]
    mime_type: "text/plain"
    aliases: ["txt", "text", ".txt"]
}

# --- Libraries ---

@entity lib_lucene : library {
    name: "Lucene.Net"
    version: "4.8.0-beta00016"
    purpose: "Full-text search engine: indexing, queries, facets, suggestions"
    aliases: ["lucene", "lucene.net", "lucenenet"]
}

@entity lib_openxml : library {
    name: "DocumentFormat.OpenXml"
    version: "3.3.0"
    purpose: "Parse and render .docx Word documents"
    aliases: ["openxml", "documentformat.openxml", "open xml"]
}

@entity lib_closedxml : library {
    name: "ClosedXML"
    version: "0.104.2"
    purpose: "Parse and render .xlsx Excel spreadsheets"
    aliases: ["closedxml", "closed xml"]
}

@entity lib_csvhelper : library {
    name: "CsvHelper"
    version: "33.0.1"
    purpose: "Parse .csv files with type mapping"
    aliases: ["csvhelper", "csv helper"]
}

@entity lib_markdig : library {
    name: "Markdig"
    version: "0.38.0"
    purpose: "Parse Markdown to HTML or plain text"
    aliases: ["markdig"]
}

@entity lib_htmlagilitypack : library {
    name: "HtmlAgilityPack"
    version: "1.11.72"
    purpose: "Parse and extract text from HTML documents"
    aliases: ["htmlagilitypack", "html agility pack", "hap"]
}

@entity lib_pdfpig : library {
    name: "PdfPig"
    version: "0.1.13"
    purpose: "Extract text from PDF documents"
    aliases: ["pdfpig", "pdf pig"]
}

# --- API Endpoints ---

@entity ep_search : endpoint {
    name: "Search"
    method: GET
    path: "/api/search"
    description: "Full-text search with DSL query syntax and faceted filtering"
    aliases: ["search endpoint", "api search"]
}

@entity ep_document : endpoint {
    name: "Get Document"
    method: GET
    path: "/api/documents/{id}"
    description: "Returns document metadata, entities, links, taxonomy assignments"
    aliases: ["document endpoint", "get document"]
}

@entity ep_index : endpoint {
    name: "Trigger Index"
    method: POST
    path: "/api/index"
    description: "Re-indexes all documents from source directory"
    aliases: ["index endpoint", "reindex", "trigger index"]
}

@entity ep_ontology : endpoint {
    name: "Get Ontology"
    method: GET
    path: "/api/ontology"
    description: "Returns full ontology: types, entities, relations, taxonomies"
    aliases: ["ontology endpoint"]
}

@entity ep_graph : endpoint {
    name: "Get Graph"
    method: GET
    path: "/api/graph"
    description: "Returns entity-document graph for visualization"
    aliases: ["graph endpoint"]
}

@entity ep_timeline : endpoint {
    name: "Get Timeline"
    method: GET
    path: "/api/timeline"
    description: "Returns chronological events for timeline visualization"
    aliases: ["timeline endpoint"]
}


# ============================================================================
# PART 3: ENTITY INSTANCES — Domain Concepts Layer (Self-Referential)
# ============================================================================

@entity concept_document : concept {
    name: "Document"
    description: "A file ingested into DockAi: parsed, indexed, searchable"
    dsl_keyword: ""
    aliases: ["document", "doc", "fichier", "file"]
}

@entity concept_entity : concept {
    name: "Entity"
    description: "A named thing (person, org, date) extracted from documents"
    dsl_keyword: "@entity"
    aliases: ["entity", "entite", "named entity"]
}

@entity concept_entity_type : concept {
    name: "Entity Type"
    description: "Schema defining an entity's properties and enums"
    dsl_keyword: "@type"
    aliases: ["entity type", "type definition", "@type"]
}

@entity concept_relation : concept {
    name: "Relation"
    description: "A directed link between two entities (e.g. employs, accuses)"
    dsl_keyword: "@relation"
    aliases: ["relation", "relationship", "lien"]
}

@entity concept_relation_type : concept {
    name: "Relation Type"
    description: "Schema defining a relation's source/target types and properties"
    dsl_keyword: "@relation.type"
    aliases: ["relation type", "relation definition", "@relation.type"]
}

@entity concept_taxonomy : concept {
    name: "Taxonomy"
    description: "Hierarchical classification tree with faceted navigation"
    dsl_keyword: "@taxonomy"
    aliases: ["taxonomy", "taxonomie", "classification", "hierarchy"]
}

@entity concept_taxonomy_node : concept {
    name: "Taxonomy Node"
    description: "A single node in a taxonomy tree (can have children, color, icon)"
    dsl_keyword: ""
    aliases: ["taxonomy node", "node", "category", "facet value"]
}

@entity concept_rule : concept {
    name: "Rule"
    description: "Pattern-driven auto-classification: regex match -> taxonomy assignment"
    dsl_keyword: "@rule"
    aliases: ["rule", "regle", "classification rule", "@rule"]
}

@entity concept_index : concept {
    name: "Index"
    description: "Lucene full-text search index with fields, facets, and suggestions"
    dsl_keyword: ""
    aliases: ["index", "search index", "lucene index"]
}

@entity concept_link : concept {
    name: "Symbolic Link"
    description: "Auto-discovered connection between documents via Jaccard similarity"
    dsl_keyword: ""
    aliases: ["symbolic link", "link", "document link", "connection"]
}

@entity concept_facet : concept {
    name: "Facet"
    description: "A filterable dimension in search results (taxonomy-based)"
    dsl_keyword: ""
    aliases: ["facet", "facette", "filter dimension"]
}

@entity concept_query : concept {
    name: "Query"
    description: "A DSL search expression: field:value, boolean, ranges, wildcards"
    dsl_keyword: ""
    aliases: ["query", "requete", "search query", "dsl query"]
}


# ============================================================================
# PART 4: ENTITY INSTANCES — Capabilities Layer
# ============================================================================

@entity cap_parse : capability {
    name: "Parse"
    description: "Convert file format (docx, pdf, xlsx, csv, md, html, txt) to searchable text"
    input: "Raw file bytes"
    output: "Plain text content + metadata"
    aliases: ["parse", "parsing", "extract text", "convert format"]
}

@entity cap_index : capability {
    name: "Index"
    description: "Build Lucene full-text index with fields, facets, and term vectors"
    input: "Document with text, entities, taxonomy assignments"
    output: "Searchable Lucene index"
    aliases: ["index", "indexing", "build index"]
}

@entity cap_extract : capability {
    name: "Extract Entities"
    description: "Find entity mentions in document text via regex alias matching"
    input: "Document text + ontology entities with aliases"
    output: "Set of matched entity IDs per document"
    aliases: ["extract", "entity extraction", "ner", "mention detection"]
}

@entity cap_classify : capability {
    name: "Classify"
    description: "Apply @rule patterns to auto-assign taxonomy nodes to documents"
    input: "Document text + classification rules"
    output: "Taxonomy assignments per document"
    aliases: ["classify", "classification", "auto classify", "rule application"]
}

@entity cap_link : capability {
    name: "Link Discovery"
    description: "Discover document connections via Jaccard similarity of shared entities/taxonomies"
    input: "All documents with their entity sets"
    output: "Symbolic links with strength scores"
    aliases: ["link", "link discovery", "jaccard", "document linking"]
}

@entity cap_search : capability {
    name: "Search"
    description: "Execute DSL queries against Lucene index with faceted filtering"
    input: "DSL query string + optional facet filters"
    output: "Ranked search results with highlights"
    aliases: ["search", "query execution", "full text search"]
}

@entity cap_render : capability {
    name: "Render"
    description: "Convert document to HTML for browser display (server-side)"
    input: "Raw document file"
    output: "HTML string"
    aliases: ["render", "rendering", "html conversion"]
}

@entity cap_visualize_graph : capability {
    name: "Graph Visualization"
    description: "Show entity-document network using vis-network force-directed layout"
    input: "Graph API response (nodes, edges)"
    output: "Interactive network visualization"
    aliases: ["graph", "network graph", "entity graph", "vis-network"]
}

@entity cap_visualize_timeline : capability {
    name: "Timeline Visualization"
    description: "Show chronological events using vis-timeline with grouped tracks"
    input: "Timeline API response (events, groups)"
    output: "Interactive timeline visualization"
    aliases: ["timeline", "chronologie", "vis-timeline"]
}


# ============================================================================
# PART 5: ENTITY INSTANCES — Forensic Layer (Qwant Affair)
# ============================================================================

# --- Persons ---

@entity erard : person {
    name: "Stephane Erard"
    role: plaignant
    organization: "Qwant (ex)"
    aliases: ["erard", "serard", "stephane", "s. erard", "stephane erard", "st erard"]
}

@entity vignaux : person {
    name: "Pierre Vignaux"
    role: developpeur
    organization: "Qwant"
    aliases: ["vignaux", "p.vignaux", "pierre vignaux", "p. vignaux", "pvignaux"]
}

@entity decaux : person {
    name: "Thomas Decaux"
    role: developpeur
    organization: "Qwant"
    aliases: ["decaux", "t.decaux", "thomas decaux", "t. decaux", "tdecaux"]
}

@entity yau : person {
    name: "Victoire Yau"
    role: dpo
    organization: "Qwant"
    aliases: ["yau", "victoire yau", "v. yau", "dpo qwant"]
}

@entity poinat : person {
    name: "Maitre Poinat"
    role: avocat
    organization: ""
    aliases: ["poinat", "me poinat", "maitre poinat", "avocate poinat"]
}

@entity cassar : person {
    name: "Jonathan Cassar"
    role: developpeur
    organization: "Qwant"
    aliases: ["cassar", "jonathan cassar", "j. cassar", "jcassar"]
}

@entity bourrelly : person {
    name: "Bourrelly"
    role: harceleur
    organization: ""
    aliases: ["bourrelly"]
}

@entity chemin : person {
    name: "Chemin"
    role: temoin
    organization: "Qwant"
    aliases: ["chemin"]
}

@entity leandri : person {
    name: "Leandri"
    role: dirigeant
    organization: "Qwant"
    aliases: ["leandri"]
}

# --- Organizations ---

@entity qwant : organization {
    name: "Qwant SAS"
    type: entreprise
    country: "France"
    aliases: ["qwant", "qwant sas", "qwant.com"]
}

@entity cnil : organization {
    name: "CNIL"
    type: autorite
    country: "France"
    aliases: ["cnil", "commission nationale informatique libertes"]
}

@entity defenseur_droits : organization {
    name: "Defenseur des Droits"
    type: autorite
    country: "France"
    aliases: ["defenseur des droits", "ddd", "ombudsman"]
}

@entity prudhommes : organization {
    name: "Conseil de Prudhommes"
    type: juridiction
    country: "France"
    aliases: ["prudhommes", "prud'hommes", "conseil prudhommes", "cph"]
}

@entity bpi : organization {
    name: "Banque Publique d'Investissement"
    type: investisseur
    country: "France"
    aliases: ["bpi", "bpifrance", "banque publique investissement"]
}

@entity cdc : organization {
    name: "Caisse des Depots"
    type: investisseur
    country: "France"
    aliases: ["cdc", "caisse des depots", "caisse depots"]
}

@entity dinum : organization {
    name: "DINUM"
    type: autorite
    country: "France"
    aliases: ["dinum", "direction interministerielle numerique"]
}

@entity linkedin : organization {
    name: "LinkedIn"
    type: plateforme
    country: "USA"
    aliases: ["linkedin"]
}

@entity bing : organization {
    name: "Bing"
    type: plateforme
    country: "USA"
    aliases: ["bing", "microsoft bing"]
}

@entity xilopix : organization {
    name: "Xilopix/Xaphir"
    type: entreprise
    country: "France"
    aliases: ["xilopix", "xaphir", "xilopix xaphir"]
}

# --- Events ---

@entity evt_antiscrap_dev : event {
    name: "Developpement anti-scraping LinkedIn"
    date: "2016-08-03"
    event_type: technique
    description: "Vignaux implemente QWANT-294: blocage cible des resultats LinkedIn, spam scoring, ban IP"
    aliases: ["anti-scraping", "qwant-294 dev", "linkedin blocking"]
}

@entity evt_api580 : event {
    name: "Filtres API-580 deployes"
    date: "2016-09-26"
    event_type: technique
    description: "Filtres anti-scrap UA specifiques deployes en production (v1.29.3)"
    aliases: ["api-580", "api580", "ua filtering"]
}

@entity evt_alerte_publique : event {
    name: "Alerte publique tweets Erard"
    date: "2016-08-25"
    event_type: alerte
    description: "Erard publie tweets d'alerte sur pseudo-anonymisation et pratiques Qwant"
    aliases: ["tweets erard", "alerte publique", "whistleblowing tweets"]
}

@entity evt_alerte_interne : event {
    name: "Alerte interne a Victoire Yau"
    date: "2016-12-19"
    event_type: alerte
    description: "Erard signale par Skype a la DPO: pseudo-anonymisation, transfert Bing, lacunes CGU"
    aliases: ["alerte interne", "skype yau", "signalement dpo"]
}

@entity evt_reponse_yau : event {
    name: "Reponse Yau: non non t'inquiete"
    date: "2016-12-19"
    event_type: alerte
    description: "DPO repond 'non non t'inquiete' - information inexacte et incomplete selon CNIL"
    aliases: ["reponse yau", "non non t'inquiete"]
}

@entity evt_licenciement : event {
    name: "Licenciement Erard"
    date: "2017-05-15"
    event_type: licenciement
    description: "Notification de licenciement pour motif reel et serieux, 9 mois apres alerte interne"
    aliases: ["licenciement", "dismissal", "renvoi", "licenciement erard"]
}

@entity evt_email_poinat : event {
    name: "Email Poinat: je ne vois pas bien"
    date: "2017-07-24"
    event_type: juridique
    description: "Poinat ecrit 'je ne vois pas bien comment vous voulez utiliser cet echange' - negligence"
    aliases: ["email poinat", "faute avocate"]
}

@entity evt_harcelement_bourrelly : event {
    name: "Campagne de harcelement Bourrelly"
    date: "2019-05-01"
    event_type: harcelement
    description: "Campagne externe coordonnee d'injures et harcelement, Bourrelly condamne ensuite"
    aliases: ["harcelement bourrelly", "injure publique", "campagne harcelement"]
}

@entity evt_decision_cnil : event {
    name: "Decision CNIL favorable"
    date: "2025-01-01"
    event_type: decision
    description: "CNIL confirme: signalement Erard legitime, reponse Yau inexacte et incomplete"
    aliases: ["decision cnil", "cnil 2025", "saisine 19005268"]
}

@entity evt_saisine_ddd : event {
    name: "Saisine Defenseur des Droits"
    date: "2026-03-02"
    event_type: juridique
    description: "Depot saisine pour investigation represailles lanceur d'alerte"
    aliases: ["saisine ddd", "defenseur droits", "saisine 2026"]
}

@entity evt_audit_dinum : event {
    name: "Audit DINUM"
    date: "2026-03-01"
    event_type: audit
    description: "Rapports d'audit DINUM sur le code source et la conformite Qwant"
    aliases: ["audit dinum", "audit code"]
}

@entity evt_fake_call_cassar : event {
    name: "Falsification code audit Cassar"
    date: "2016-01-01"
    event_type: technique
    description: "Cassar ecrit 'J'ai remplace par un call fake' - branche demo avec appels falsifies"
    aliases: ["fake call", "call fake", "falsification code", "branche demo"]
}

# --- Legal References ---

@entity loi_sapin2 : legal_reference {
    name: "Protection lanceur d'alerte Sapin II"
    code: code_travail
    article: "L.1132-3-3 al.1"
    description: "Protection contre represailles pour signalement de delits en bonne foi"
    aliases: ["sapin ii", "sapin 2", "l.1132-3-3", "lanceur alerte", "whistleblower protection"]
}

@entity art_escroquerie : legal_reference {
    name: "Escroquerie"
    code: code_penal
    article: "313-1"
    description: "Tromperie des investisseurs publics sur la realite du code source"
    aliases: ["escroquerie", "article 313-1", "art 313-1", "fraude"]
}

@entity art_faux : legal_reference {
    name: "Faux et usage de faux"
    code: code_penal
    article: "441-1"
    description: "Falsification du code source pour audit (branche demo, fake calls)"
    aliases: ["faux", "usage de faux", "article 441-1", "art 441-1", "falsification"]
}

@entity art_donnees : legal_reference {
    name: "Atteinte aux donnees personnelles"
    code: code_penal
    article: "226-16 a 226-24"
    description: "Violation du traitement des donnees personnelles: pseudo-anonymisation inadequate"
    aliases: ["atteinte donnees", "226-16", "226-24", "donnees personnelles"]
}

@entity rgpd : legal_reference {
    name: "RGPD"
    code: rgpd
    article: "Reglement UE 2016/679"
    description: "Distinction pseudo-anonymisation vs anonymisation, droits des personnes"
    aliases: ["rgpd", "gdpr", "reglement europeen", "protection donnees"]
}

@entity directive_slapp : legal_reference {
    name: "Directive Anti-SLAPP"
    code: directive_ue
    article: "2024/1069"
    description: "Protection contre poursuites-baillons strategiques"
    aliases: ["slapp", "anti-slapp", "directive 2024/1069", "poursuite baillon"]
}

@entity art_nullite : legal_reference {
    name: "Nullite du licenciement"
    code: code_travail
    article: "L.1132-4"
    description: "Licenciement nul = pas de plafond Macron, reintegration + 6 mois minimum"
    aliases: ["nullite", "licenciement nul", "l.1132-4", "sans plafond macron"]
}

# --- JIRA Tickets (Evidence) ---

@entity ticket_qwant294 : jira_ticket {
    name: "QWANT-294"
    key: "QWANT-294"
    commit_count: 13
    description: "Anti-scraping LinkedIn: blocage cible, spam scoring, ban IP, cache key"
    aliases: ["qwant-294", "qwant294"]
}

@entity ticket_api580 : jira_ticket {
    name: "API-580"
    key: "API-580"
    commit_count: 4
    description: "Anti-scrap: ban user-agents specifiques"
    aliases: ["api-580", "api580"]
}

@entity ticket_api585 : jira_ticket {
    name: "API-585"
    key: "API-585"
    commit_count: 2
    description: "Anti-scrap: methodes non specifiees (masquees)"
    aliases: ["api-585", "api585"]
}

@entity ticket_api587 : jira_ticket {
    name: "API-587"
    key: "API-587"
    commit_count: 1
    description: "Anti-scrap: filtre sur controleurs specifiques"
    aliases: ["api-587", "api587"]
}

# --- Key Evidence ---

@entity ev_skype_yau : evidence {
    name: "Conversation Skype Erard-Yau"
    evidence_type: document
    date: "2016-12-19"
    source: "Capture Skype"
    description: "Alerte interne: pseudo-anonymisation, transfert Bing, CGU lacunaires"
    aliases: ["skype yau", "conversation skype", "alerte skype"]
}

@entity ev_email_poinat : evidence {
    name: "Email Poinat 24/07/2017"
    evidence_type: email
    date: "2017-07-24"
    source: "Correspondance avocat"
    description: "Poinat: 'je ne vois pas bien comment vous voulez utiliser cet echange'"
    aliases: ["email poinat juillet", "je ne vois pas bien"]
}

@entity ev_commits_antiscrap : evidence {
    name: "20 commits anti-scraping"
    evidence_type: commit
    date: "2016-08-03"
    source: "Git repository Qwant API"
    description: "20 commits sur 4 tickets JIRA, deployes en 89 versions production"
    aliases: ["commits anti-scrap", "git commits", "code source anti-scraping"]
}

@entity ev_email_cassar : evidence {
    name: "Email Cassar fake call"
    evidence_type: email
    date: "2016-01-01"
    source: "Correspondance interne Qwant"
    description: "Cassar: 'J'ai remplace par un call fake' - falsification pour audit CDC/BPI"
    aliases: ["email cassar", "call fake", "fake call email"]
}

@entity ev_sms_chemin : evidence {
    name: "SMS Chemin"
    evidence_type: sms
    date: "2019-01-01"
    source: "SMS personnel"
    description: "Chemin: 'arrete la diffamation' pendant arret maladie Erard"
    aliases: ["sms chemin", "arrete la diffamation"]
}

@entity ev_tweets_erard : evidence {
    name: "Tweets d'alerte Erard"
    evidence_type: tweet
    date: "2016-08-25"
    source: "Twitter"
    description: "Publication d'alerte sur pseudo-anonymisation et pratiques Qwant"
    aliases: ["tweets erard", "twitter erard", "alerte twitter"]
}

@entity ev_decision_cnil : evidence {
    name: "Decision CNIL n.19005268"
    evidence_type: document
    date: "2025-01-01"
    source: "CNIL"
    description: "Signalement legitime, reponse Yau inexacte et incomplete"
    aliases: ["decision cnil", "saisine 19005268", "cnil decision"]
}

@entity ev_audit_dinum : evidence {
    name: "Rapport audit DINUM"
    evidence_type: audit
    date: "2026-03-01"
    source: "DINUM"
    description: "Audit code source et conformite Qwant"
    aliases: ["audit dinum", "rapport dinum"]
}


# ============================================================================
# PART 6: RELATION TYPE DEFINITIONS
# ============================================================================

# --- Architecture relations ---

@relation.type contains {
    from: solution
    to: [project, module]
    properties: []
    inverse: contained_in
}

@relation.type implements {
    from: module
    to: component
    properties: []
    inverse: implemented_by
}

@relation.type depends_on {
    from: component
    to: library
    properties: []
    inverse: used_by
}

@relation.type parses_format {
    from: component
    to: file_format
    properties: []
}

@relation.type renders_format {
    from: component
    to: file_format
    properties: []
}

@relation.type exposes {
    from: project
    to: endpoint
    properties: []
}

@relation.type calls {
    from: component
    to: component
    properties: [method, description]
    inverse: called_by
}

@relation.type proxies_to {
    from: project
    to: project
    properties: [proxy_path]
}

# --- Domain relations ---

@relation.type realizes {
    from: component
    to: capability
    properties: []
}

@relation.type operates_on {
    from: capability
    to: concept
    properties: []
}

@relation.type defined_by_dsl {
    from: concept
    to: concept
    properties: []
}

# --- Forensic relations ---

@relation.type employed_by {
    from: person
    to: organization
    properties: [start_date, end_date, position]
    inverse: employs
}

@relation.type accused_of {
    from: person
    to: event
    properties: [basis, evidence]
}

@relation.type filed_against {
    from: person
    to: organization
    properties: [court, date, reference]
}

@relation.type harassed {
    from: person
    to: person
    properties: [period, outcome]
}

@relation.type decided_by {
    from: event
    to: organization
    properties: [date, ruling]
}

@relation.type violated {
    from: organization
    to: legal_reference
    properties: [description]
}

@relation.type proves {
    from: evidence
    to: event
    properties: [strength, description]
}

@relation.type authored_by {
    from: jira_ticket
    to: person
    properties: [commit_count]
}

@relation.type invested_in {
    from: organization
    to: organization
    properties: [role]
}

@relation.type targets {
    from: jira_ticket
    to: organization
    properties: [description]
}

@relation.type protects {
    from: legal_reference
    to: person
    properties: [mechanism]
}

# --- Auto relations ---

@relation.type mentions {
    from: concept
    to: concept
    auto: true
}

@relation.type shared_context {
    from: concept
    to: concept
    symmetric: true
    auto: true
}


# ============================================================================
# PART 7: RELATION INSTANCES — Architecture
# ============================================================================

# Solution contains projects
@relation dockai_sln -[contains]-> dockai_api {}
@relation dockai_sln -[contains]-> dockai_viewer {}

# Projects contain modules
@relation dockai_api -[contains]-> mod_dsl {}
@relation dockai_api -[contains]-> mod_parsing {}
@relation dockai_api -[contains]-> mod_indexing {}
@relation dockai_api -[contains]-> mod_analysis {}
@relation dockai_api -[contains]-> mod_search {}
@relation dockai_api -[contains]-> mod_ontology {}
@relation dockai_viewer -[contains]-> mod_rendering {}
@relation dockai_viewer -[contains]-> mod_frontend {}

# Modules implement components
@relation mod_dsl -[implements]-> dsl_lexer {}
@relation mod_dsl -[implements]-> dsl_schema_parser {}
@relation mod_parsing -[implements]-> parser_factory {}
@relation mod_parsing -[implements]-> docx_parser {}
@relation mod_parsing -[implements]-> xlsx_parser {}
@relation mod_parsing -[implements]-> csv_parser {}
@relation mod_parsing -[implements]-> pdf_parser {}
@relation mod_parsing -[implements]-> md_parser {}
@relation mod_parsing -[implements]-> html_parser {}
@relation mod_parsing -[implements]-> txt_parser {}
@relation mod_indexing -[implements]-> lucene_indexer {}
@relation mod_indexing -[implements]-> index_manager {}
@relation mod_analysis -[implements]-> entity_extractor {}
@relation mod_analysis -[implements]-> rule_engine {}
@relation mod_analysis -[implements]-> link_builder {}
@relation mod_search -[implements]-> search_service {}
@relation mod_search -[implements]-> dsl_query_parser {}
@relation mod_ontology -[implements]-> ontology_manager {}
@relation mod_rendering -[implements]-> renderer_factory {}
@relation mod_frontend -[implements]-> api_client_js {}

# Component -> library dependencies
@relation docx_parser -[depends_on]-> lib_openxml {}
@relation xlsx_parser -[depends_on]-> lib_closedxml {}
@relation csv_parser -[depends_on]-> lib_csvhelper {}
@relation pdf_parser -[depends_on]-> lib_pdfpig {}
@relation md_parser -[depends_on]-> lib_markdig {}
@relation html_parser -[depends_on]-> lib_htmlagilitypack {}
@relation lucene_indexer -[depends_on]-> lib_lucene {}
@relation search_service -[depends_on]-> lib_lucene {}

# Component -> format mappings
@relation docx_parser -[parses_format]-> fmt_docx {}
@relation xlsx_parser -[parses_format]-> fmt_xlsx {}
@relation csv_parser -[parses_format]-> fmt_csv {}
@relation pdf_parser -[parses_format]-> fmt_pdf {}
@relation md_parser -[parses_format]-> fmt_md {}
@relation html_parser -[parses_format]-> fmt_html {}
@relation txt_parser -[parses_format]-> fmt_txt {}

# Viewer proxies to API
@relation dockai_viewer -[proxies_to]-> dockai_api {
    proxy_path: "/proxy"
}

# API exposes endpoints
@relation dockai_api -[exposes]-> ep_search {}
@relation dockai_api -[exposes]-> ep_document {}
@relation dockai_api -[exposes]-> ep_index {}
@relation dockai_api -[exposes]-> ep_ontology {}
@relation dockai_api -[exposes]-> ep_graph {}
@relation dockai_api -[exposes]-> ep_timeline {}

# Component call graph
@relation index_manager -[calls]-> parser_factory {
    method: "GetParser"
    description: "Select parser for document format"
}
@relation index_manager -[calls]-> entity_extractor {
    method: "Extract"
    description: "Find entity mentions in parsed text"
}
@relation index_manager -[calls]-> rule_engine {
    method: "Apply"
    description: "Auto-classify via regex rules"
}
@relation index_manager -[calls]-> link_builder {
    method: "Build"
    description: "Discover links via Jaccard similarity"
}
@relation index_manager -[calls]-> lucene_indexer {
    method: "Index"
    description: "Write to Lucene index"
}
@relation dsl_query_parser -[calls]-> search_service {
    method: "Search"
    description: "Execute parsed query"
}
@relation api_client_js -[calls]-> search_service {
    method: "fetch /proxy/api/search"
    description: "Browser calls via proxy"
}


# ============================================================================
# PART 8: RELATION INSTANCES — Domain (Self-Referential)
# ============================================================================

# Components realize capabilities
@relation parser_factory -[realizes]-> cap_parse {}
@relation lucene_indexer -[realizes]-> cap_index {}
@relation entity_extractor -[realizes]-> cap_extract {}
@relation rule_engine -[realizes]-> cap_classify {}
@relation link_builder -[realizes]-> cap_link {}
@relation search_service -[realizes]-> cap_search {}
@relation renderer_factory -[realizes]-> cap_render {}

# Capabilities operate on concepts
@relation cap_parse -[operates_on]-> concept_document {}
@relation cap_index -[operates_on]-> concept_index {}
@relation cap_extract -[operates_on]-> concept_entity {}
@relation cap_classify -[operates_on]-> concept_taxonomy_node {}
@relation cap_link -[operates_on]-> concept_link {}
@relation cap_search -[operates_on]-> concept_query {}
@relation cap_render -[operates_on]-> concept_document {}

# DSL constructs define concepts
@relation concept_entity_type -[defined_by_dsl]-> concept_entity {}
@relation concept_relation_type -[defined_by_dsl]-> concept_relation {}
@relation concept_taxonomy -[defined_by_dsl]-> concept_taxonomy_node {}
@relation concept_rule -[defined_by_dsl]-> concept_taxonomy_node {}


# ============================================================================
# PART 9: RELATION INSTANCES — Forensic (Qwant Affair)
# ============================================================================

# Employment
@relation erard -[employed_by]-> qwant {
    start_date: "2015"
    end_date: "2017-05"
    position: "Ingenieur R&D"
}

@relation vignaux -[employed_by]-> qwant {
    start_date: "2016"
    end_date: ""
    position: "Lead Developer API"
}

@relation decaux -[employed_by]-> qwant {
    start_date: "2016"
    end_date: ""
    position: "Developer DataHub"
}

@relation yau -[employed_by]-> qwant {
    start_date: "2016"
    end_date: ""
    position: "DPO / Legal Officer"
}

@relation cassar -[employed_by]-> qwant {
    start_date: "2015"
    end_date: ""
    position: "Developer"
}

# Legal filings
@relation erard -[filed_against]-> qwant {
    court: "Prudhommes"
    date: "2017"
    reference: "Licenciement nul lanceur alerte"
}

@relation erard -[filed_against]-> cnil {
    court: "CNIL"
    date: "2019"
    reference: "Saisine n.19005268"
}

# Harassment
@relation bourrelly -[harassed]-> erard {
    period: "2019-05 onwards"
    outcome: "Condamne pour injure publique"
}

# Legal violations by Qwant
@relation qwant -[violated]-> art_donnees {
    description: "Pseudo-anonymisation inadequate, transfert Bing sans consentement"
}

@relation qwant -[violated]-> rgpd {
    description: "Confusion pseudo-anonymisation vs anonymisation, CGU lacunaires"
}

@relation qwant -[violated]-> art_escroquerie {
    description: "Tromperie investisseurs publics BPI/CDC sur realite code source"
}

@relation qwant -[violated]-> art_faux {
    description: "Branche demo avec appels falsifies pour audit"
}

@relation qwant -[violated]-> loi_sapin2 {
    description: "Represailles contre lanceur d'alerte: licenciement 9 mois apres signalement"
}

# Evidence proves events
@relation ev_commits_antiscrap -[proves]-> evt_antiscrap_dev {
    strength: "fort"
    description: "20 commits traces dans git avec dates, auteurs, tickets JIRA"
}

@relation ev_skype_yau -[proves]-> evt_alerte_interne {
    strength: "fort"
    description: "Capture directe de la conversation d'alerte"
}

@relation ev_email_poinat -[proves]-> evt_email_poinat {
    strength: "fort"
    description: "Email original attestant la negligence professionnelle"
}

@relation ev_email_cassar -[proves]-> evt_fake_call_cassar {
    strength: "fort"
    description: "Aveu ecrit de falsification de code pour audit"
}

@relation ev_sms_chemin -[proves]-> evt_harcelement_bourrelly {
    strength: "moyen"
    description: "Contexte de pression pendant arret maladie"
}

@relation ev_tweets_erard -[proves]-> evt_alerte_publique {
    strength: "fort"
    description: "Publication horodatee d'alerte publique"
}

@relation ev_decision_cnil -[proves]-> evt_decision_cnil {
    strength: "fort"
    description: "Decision officielle de l'autorite de controle"
}

@relation ev_audit_dinum -[proves]-> evt_audit_dinum {
    strength: "fort"
    description: "Rapport d'audit officiel"
}

# JIRA tickets authored by developers
@relation ticket_qwant294 -[authored_by]-> vignaux {
    commit_count: "13"
}

@relation ticket_api580 -[authored_by]-> vignaux {
    commit_count: "4"
}

@relation ticket_api585 -[authored_by]-> vignaux {
    commit_count: "2"
}

@relation ticket_api587 -[authored_by]-> vignaux {
    commit_count: "1"
}

# JIRA tickets target LinkedIn
@relation ticket_qwant294 -[targets]-> linkedin {
    description: "Blocage cible des resultats LinkedIn dans les recherches Qwant"
}

# Investments
@relation bpi -[invested_in]-> qwant {
    role: "Investisseur public"
}

@relation cdc -[invested_in]-> qwant {
    role: "Investisseur public via filiale"
}

# Legal protection
@relation loi_sapin2 -[protects]-> erard {
    mechanism: "Nullite du licenciement sans plafond Macron"
}

@relation art_nullite -[protects]-> erard {
    mechanism: "Reintegration + 6 mois minimum indemnite"
}

# Decisions
@relation evt_decision_cnil -[decided_by]-> cnil {
    date: "2025"
    ruling: "Signalement Erard legitime, reponse Yau inexacte"
}

@relation evt_saisine_ddd -[decided_by]-> defenseur_droits {
    date: "2026-03-02"
    ruling: "En cours d'instruction"
}


# ============================================================================
# PART 10: TAXONOMIES
# ============================================================================

@taxonomy layer {
    label: "Couche architecturale"
    facet: true

    dsl "DSL Engine" { color: "#9B59B6", desc: "Lexer, parsers, schema validation" }
    parsing "Document Parsing" { color: "#E67E22", desc: "Format-specific text extraction" }
    indexing "Indexing" { color: "#2ECC71", desc: "Lucene full-text index management" }
    analysis "Analysis" { color: "#E74C3C", desc: "Entity extraction, classification, link discovery" }
    search "Search" { color: "#3498DB", desc: "Query parsing and execution" }
    ontology "Ontology" { color: "#1ABC9C", desc: "Type, entity, relation, taxonomy stores" }
    rendering "Rendering" { color: "#F39C12", desc: "Server-side document-to-HTML conversion" }
    frontend "Frontend" { color: "#8E44AD", desc: "Browser-side views and interactions" }
}

@taxonomy concern {
    label: "Domaine technique"
    facet: true

    backend "Backend" { color: "#2C3E50", desc: "C# / ASP.NET Core / Lucene" }
    frontend_concern "Frontend" { color: "#8E44AD", desc: "JavaScript / HTML / CSS" }
    shared "Shared" { color: "#7F8C8D", desc: "Models, DSL, configuration" }
}

@taxonomy dsl_family {
    label: "Famille DSL"
    facet: true

    schema_def "Schema Definition" { color: "#9B59B6", desc: "@type and @relation.type — defining structure" }
    instance_data "Instance Data" { color: "#27AE60", desc: "@entity and @relation — populating data" }
    automation "Automation" { color: "#E74C3C", desc: "@rule — pattern-driven classification" }
    classification "Classification" { color: "#F39C12", desc: "@taxonomy — hierarchical organization" }
    query_lang "Query Language" { color: "#3498DB", desc: "DSL search syntax — retrieving data" }
}

@taxonomy lifecycle {
    label: "Etape du pipeline"
    facet: true

    ingest "Ingestion" { color: "#95A5A6", desc: "Decouverte et lecture des fichiers source" }
    parse_step "Parsing" { color: "#E67E22", desc: "Conversion format vers texte brut" }
    analyze_step "Analysis" { color: "#E74C3C", desc: "Extraction entites + classification + liens" }
    index_step "Indexing" { color: "#2ECC71", desc: "Ecriture dans l'index Lucene" }
    serve_step "Serving" { color: "#3498DB", desc: "Recherche + rendu + visualisation" }
}

# --- Forensic taxonomies ---

@taxonomy track {
    label: "Piste juridique"
    facet: true

    voie_penale "Voie penale" { color: "#E74C3C", desc: "Plaintes penales: escroquerie, faux, atteinte donnees" }
    voie_prudhomale "Voie prudhomale" { color: "#3498DB", desc: "Licenciement nul, lanceur alerte Sapin II" }
    voie_avocate "Voie responsabilite avocate" { color: "#F39C12", desc: "Faute professionnelle Me Poinat" }
    voie_cnil "Voie CNIL/RGPD" { color: "#2ECC71", desc: "Saisine CNIL, violations donnees personnelles" }
    voie_medias "Voie medias" { color: "#9B59B6", desc: "Communication publique, dossier presse" }
}

@taxonomy evidence_strength {
    label: "Force probante"
    facet: true

    direct "Preuve directe" { color: "#27AE60", desc: "Document original, aveu ecrit, decision officielle" }
    indirect "Preuve indirecte" { color: "#F39C12", desc: "Faisceau d'indices, correlation temporelle" }
    contextual "Element de contexte" { color: "#95A5A6", desc: "Temoignage, analyse financiere, rapport" }
}

@taxonomy doc_category {
    label: "Categorie de document"
    facet: true

    strategie "Strategie" { color: "#E74C3C", desc: "Plans juridiques et strategiques" }
    juridique "Juridique" { color: "#3498DB", desc: "Notes de droit, analyses juridiques" }
    preuves "Preuves" { color: "#27AE60", desc: "Pieces justificatives, captures, commits" }
    technique "Technique" { color: "#9B59B6", desc: "Analyse code source, audits techniques" }
    financier "Financier" { color: "#F39C12", desc: "Matrices prejudices, projections salaires" }
    medical "Medical" { color: "#E67E22", desc: "Certificats, arrets maladie, hospitalisations" }
    media "Media" { color: "#8E44AD", desc: "Dossiers presse, tribunes, communiques" }
    personnel "Personnel" { color: "#2C3E50", desc: "CV, listes personnel, organigrammes" }
    correspondance "Correspondance" { color: "#1ABC9C", desc: "Emails, SMS, lettres avocats" }
    archive "Archive" { color: "#7F8C8D", desc: "Wayback Machine, captures web" }
    synthese "Synthese" { color: "#34495E", desc: "Bilans, inventaires, recapitulatifs" }
    administratif "Administratif" { color: "#BDC3C7", desc: "Saisines, demandes officielles" }
}


# ============================================================================
# PART 11: CLASSIFICATION RULES
# ============================================================================

# --- Architecture rules (classify DockAi source files) ---

@rule auto_layer_dsl {
    when content matches "DslToken|DslLexer|DslSchema|@type|@entity|@relation|@taxonomy|@rule"
    then assign layer: dsl
    confidence: 0.8
}

@rule auto_layer_parsing {
    when content matches "IDocumentParser|ParseAsync|DocumentParser|ParserFactory"
    then assign layer: parsing
    confidence: 0.8
}

@rule auto_layer_indexing {
    when content matches "Lucene\.Net|IndexWriter|IndexSearcher|DirectoryReader|LuceneIndexer"
    then assign layer: indexing
    confidence: 0.8
}

@rule auto_layer_analysis {
    when content matches "EntityExtractor|RuleEngine|LinkBuilder|Jaccard|ClassificationRule"
    then assign layer: analysis
    confidence: 0.8
}

@rule auto_layer_search {
    when content matches "SearchService|DslQueryParser|LuceneQuery|BooleanQuery"
    then assign layer: search
    confidence: 0.8
}

@rule auto_layer_rendering {
    when content matches "IDocumentRenderer|RenderAsync|DocumentRenderer|RendererFactory"
    then assign layer: rendering
    confidence: 0.8
}

@rule auto_layer_ontology {
    when content matches "OntologyStore|TaxonomyStore|OntologyManager|PropertyDef"
    then assign layer: ontology
    confidence: 0.8
}

@rule auto_layer_frontend {
    when content matches "import.*from|addEventListener|querySelector|fetch\(|document\."
    then assign layer: frontend
    confidence: 0.7
}

@rule auto_concern_backend {
    when content matches "namespace DockAi|using Microsoft\.AspNetCore|app\.Map|builder\.Services"
    then assign concern: backend
    confidence: 0.8
}

@rule auto_concern_frontend {
    when content matches "export class|document\.getElementById|\.innerHTML|\.onclick"
    then assign concern: frontend_concern
    confidence: 0.8
}

# --- Forensic rules (classify Qwant affair documents) ---

@rule auto_track_penal {
    when content matches "plainte|escroquerie|abus de confiance|penal|code penal|faux et usage|313-1|441-1"
    then assign track: voie_penale
    confidence: 0.7
}

@rule auto_track_prudhommes {
    when content matches "prudhommes|licenciement|lanceur d.alerte|sapin|L\.1132|nullite|indemnite"
    then assign track: voie_prudhomale
    confidence: 0.7
}

@rule auto_track_avocate {
    when content matches "poinat|faute avocate|responsabilite professionnelle|batonnier|negligence"
    then assign track: voie_avocate
    confidence: 0.7
}

@rule auto_track_cnil {
    when content matches "cnil|rgpd|donnees personnelles|pseudo.anonymisation|vie privee|226-16"
    then assign track: voie_cnil
    confidence: 0.7
}

@rule auto_track_medias {
    when content matches "communique|presse|tribune|media|journaliste|dossier presse|publication"
    then assign track: voie_medias
    confidence: 0.6
}

@rule auto_doc_strategie {
    when content matches "strategie|voie juridique|plan d.action|calendrier|feuille de route"
    then assign doc_category: strategie
    confidence: 0.6
}

@rule auto_doc_preuves {
    when content matches "commit|git log|QWANT-294|API-580|API-585|API-587|capture|screenshot"
    then assign doc_category: preuves
    confidence: 0.8
}

@rule auto_doc_technique {
    when content matches "code source|sonarqube|audit|api tag|anti.scrap|scraping|spam.score"
    then assign doc_category: technique
    confidence: 0.7
}

@rule auto_doc_financier {
    when content matches "prejudice|indemnite|salaire|projection|matrice|calculateur|diane|financier"
    then assign doc_category: financier
    confidence: 0.7
}

@rule auto_doc_medical {
    when content matches "certificat medical|arret maladie|hospitalisation|cpam|psychiatre"
    then assign doc_category: medical
    confidence: 0.8
}

@rule auto_doc_correspondance {
    when content matches "email|courrier|lettre|sms|skype|correspondance|mise en demeure"
    then assign doc_category: correspondance
    confidence: 0.6
}

@rule auto_doc_media {
    when content matches "communique de presse|dossier presse|tribune|mediapart|le media"
    then assign doc_category: media
    confidence: 0.7
}

@rule auto_evidence_direct {
    when content matches "piece n|annexe|original|capture ecran|aveu|decision officielle"
    then assign evidence_strength: direct
    confidence: 0.6
}

@rule auto_evidence_indirect {
    when content matches "faisceau|correlation|chronologie|proximite temporelle|concordance"
    then assign evidence_strength: indirect
    confidence: 0.5
}
