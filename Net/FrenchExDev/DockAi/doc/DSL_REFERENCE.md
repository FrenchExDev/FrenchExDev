# DockAi DSL Reference

A domain-specific language for defining ontologies, taxonomies, and querying document indexes.

The DSL has two modes:
- **Schema mode**: Define entity types, entities, relations, taxonomies, and rules in `.dsl` files
- **Query mode**: Search the index using ontology-aware field filters, boolean logic, and graph traversal

---

## Table of Contents

1. [Schema DSL](#1-schema-dsl)
   - [Entity Types](#11-entity-types-type)
   - [Entity Instances](#12-entity-instances-entity)
   - [Relation Types](#13-relation-types-relationtype)
   - [Relation Instances](#14-relation-instances-relation)
   - [Taxonomies](#15-taxonomies-taxonomy)
   - [Rules](#16-auto-classification-rules-rule)
2. [Query DSL](#2-query-dsl)
   - [Full-text Search](#21-full-text-search)
   - [Field Filters](#22-field-filters)
   - [Ontology Filters](#23-ontology-filters)
   - [Taxonomy Filters](#24-taxonomy-filters)
   - [Boolean Operators](#25-boolean-operators)
   - [Symbolic Link Queries](#26-symbolic-link-queries)
   - [Graph Traversal](#27-graph-traversal)
   - [Facets & Aggregation](#28-facets--aggregation)
3. [Formal Grammar](#3-formal-grammar)
   - [Schema Grammar (EBNF)](#31-schema-grammar-ebnf)
   - [Query Grammar (EBNF)](#32-query-grammar-ebnf)
4. [Data Types](#4-data-types)
5. [Complete Example](#5-complete-example-ontologydsl)

---

## 1. Schema DSL

Schema definitions live in `.dsl` files. Lines starting with `#` are comments.

### 1.1 Entity Types (`@type`)

Define a class of entities with typed properties.

**Syntax:**
```
@type <type_id> {
    <property>: <data_type>
    <property>: enum [<value1>, <value2>, ...]
    <property>: <data_type>[]
}
```

**Supported property types:** `string`, `number`, `date`, `bool`, `enum [...]`, and arrays with `[]` suffix.

**Examples:**
```dsl
@type person {
    name: string
    role: enum [plaintiff, defendant, witness, accomplice, executive, journalist, lawyer, politician]
    aliases: string[]
    birth_date: date
}

@type organization {
    name: string
    role: enum [defendant, acquired_company, partner]
    aliases: string[]
    registration_number: string
}

@type institution {
    name: string
    role: enum [authority, investor, regulator]
    aliases: string[]
}

@type technology {
    name: string
    role: enum [search_engine, tool, platform]
    aliases: string[]
}

@type legal_reference {
    code: string
    article: string
    description: string
}

@type monetary_amount {
    value: number
    currency: string
    context: string
}

@type date_event {
    date: date
    label: string
    significance: string
}
```

**Semantics:**
- `type_id` must be unique across all types
- Properties are name-value pairs
- `enum` restricts values to a predefined set
- Array types (`string[]`) allow multiple values
- Types define the schema; instances are created with `@entity`

---

### 1.2 Entity Instances (`@entity`)

Create an instance of a defined type.

**Syntax:**
```
@entity <entity_id> : <type_id> {
    <property>: <value>
    <property>: [<value1>, <value2>, ...]
}
```

**Examples:**
```dsl
@entity erard : person {
    name: "Stephane Erard"
    role: plaintiff
    aliases: ["erard", "serard", "stephane", "s. erard", "stephane erard"]
}

@entity qwant : organization {
    name: "Qwant SAS"
    role: defendant
    aliases: ["qwant", "qwant sas", "qwant.com"]
}

@entity cnil : institution {
    name: "CNIL"
    role: authority
    aliases: ["cnil", "commission nationale informatique et libertes"]
}

@entity bing : technology {
    name: "Microsoft Bing"
    role: search_engine
    aliases: ["bing", "microsoft bing", "bing api"]
}
```

**Semantics:**
- `entity_id` must be unique across all entities
- The type must be previously defined via `@type`
- String values use double quotes; enum values are bare identifiers
- Arrays use bracket notation: `[value1, value2]`
- The `aliases` property is special: it's used by the indexer for entity detection in document content

---

### 1.3 Relation Types (`@relation.type`)

Define a typed predicate describing how entities connect.

**Syntax:**
```
@relation.type <relation_type_id> {
    from: <type_id> | [<type_id>, ...]
    to: <type_id> | [<type_id>, ...]
    properties: [<prop1>, <prop2>, ...]
    inverse: <inverse_relation_type_id>       # optional
    auto: true | false                         # optional, default false
    symmetric: true | false                    # optional, default false
}
```

**Examples:**
```dsl
@relation.type employs {
    from: organization
    to: person
    properties: [start_date, end_date, position]
    inverse: employed_by
}

@relation.type accuses {
    from: person
    to: [person, organization]
    properties: [reason, date, severity]
    inverse: accused_by
}

@relation.type directs {
    from: person
    to: organization
    properties: [start_date, end_date, title]
    inverse: directed_by
}

@relation.type represents {
    from: person
    to: person
    properties: [mandate_type, start_date]
    inverse: represented_by
}

@relation.type invests_in {
    from: institution
    to: organization
    properties: [amount, date, investment_type]
    inverse: funded_by
}

@relation.type acquired {
    from: organization
    to: organization
    properties: [date, amount, conditions]
    inverse: acquired_by
}

@relation.type uses {
    from: organization
    to: technology
    properties: [start_date, context]
}

@relation.type regulates {
    from: institution
    to: [person, organization]
    properties: [procedure_type, case_number]
    inverse: regulated_by
}

# Auto-detected relations (built by the indexer)

@relation.type mentions {
    from: document
    to: [person, organization, institution, technology, legal_reference]
    auto: true
    properties: [count, positions]
}

@relation.type cites {
    from: document
    to: legal_reference
    auto: true
    properties: [context]
}

@relation.type shared_context {
    from: document
    to: document
    auto: true
    symmetric: true
    properties: [via_entities, via_themes, strength]
}
```

**Semantics:**
- `from` / `to` constrain which entity types can participate
- `from: document` is a reserved type representing indexed documents
- `properties` lists the property names that relation instances can carry
- `inverse` creates a named reverse direction (e.g., `employs` / `employed_by`)
- `auto: true` means the indexer will automatically create instances of this relation during indexing
- `symmetric: true` means `A→B` implies `B→A`

---

### 1.4 Relation Instances (`@relation`)

Create a directed relation between two entities.

**Syntax:**
```
@relation <from_entity_id> -[<relation_type_id>]-> <to_entity_id> {
    <property>: <value>
    ...
}
```

**Examples:**
```dsl
@relation erard -[employed_by]-> qwant {
    start_date: "2017-01"
    end_date: "2019-07"
    position: "Senior R&D Engineer"
}

@relation leandri -[directs]-> qwant {
    start_date: "2013"
    end_date: "2020"
    title: "CEO co-founder"
}

@relation poinat -[represents]-> erard {
    mandate_type: "labor_court"
    start_date: "2019"
}

@relation bpi -[invests_in]-> qwant {
    amount: "25000000"
    investment_type: "public_funds"
}

@relation qwant -[acquired]-> xilopix {
    date: "2017"
    conditions: "technology absorption"
}

@relation qwant -[uses]-> bing {
    start_date: "2019"
    context: "repackaged search results as proprietary"
}

@relation erard -[accuses]-> qwant {
    reason: "technical_fraud"
    severity: "high"
}

@relation erard -[accuses]-> leandri {
    reason: "moral_harassment"
    severity: "high"
}

@relation cnil -[regulates]-> qwant {
    procedure_type: "gdpr_complaint"
    case_number: "19005268"
}
```

**Semantics:**
- The arrow `-[...]->` syntax is always left-to-right
- Both entity IDs must be previously defined via `@entity`
- The relation type must be previously defined via `@relation.type`
- Entity types must match the `from` / `to` constraints of the relation type
- If the relation type has an `inverse`, the inverse relation is automatically available for querying
- Properties are free-form key-value pairs; they don't need to match a schema

---

### 1.5 Taxonomies (`@taxonomy`)

Define hierarchical classification trees with optional faceting.

**Syntax:**
```
@taxonomy <taxonomy_id> {
    label: <string>
    facet: true | false                     # optional, default false

    <node_id> <label> {
        <property>: <value>
        <child_node_id> <label> {
            <property>: <value>
        }
    }
}
```

**Examples:**
```dsl
@taxonomy track {
    label: "Legal Track"
    facet: true

    voie_a "Criminal Track" {
        color: "#E74C3C"
        desc: "Criminal complaints, fraud, breach of trust"
    }
    voie_b "Labor Track" {
        color: "#3498DB"
        desc: "Unfair dismissal, moral harassment, Syntec 3.3 reclassification"
    }
    voie_c "Lawyer Track" {
        color: "#9B59B6"
        desc: "Professional liability Me Poinat, bar association complaint"
    }
    cnil_rgpd "CNIL/GDPR" {
        color: "#F39C12"
        desc: "Personal data protection, CNIL complaint"
    }
    media "Media Strategy" {
        color: "#1ABC9C"
        desc: "Press releases, press kits, op-eds"
    }
}

@taxonomy theme {
    label: "Theme"
    facet: true

    fraud "Fraud" {
        fraud_tech "Technical Fraud" {
            desc: "Bing repackaging, fake search engine, DINUM audit"
        }
        financial "Financial Misappropriation" {
            desc: "BPI/CDC, excessive salaries, public funds fraud"
        }
    }
    labor_law "Labor Law" {
        employment "Employment" {
            desc: "Unfair dismissal, Syntec position 3.3, reclassification"
        }
        harassment "Harassment" {
            desc: "Moral harassment, pressure, intimidation, SLAPP"
        }
    }
    whistleblower "Whistleblower" {
        desc: "Whistleblower protection, retaliation, Sapin II law"
    }
    gdpr "GDPR/Data" {
        desc: "GDPR violations, personal data, CNIL complaint"
    }
    evidence "Evidence & Forensics" {
        desc: "Git analysis, screenshots, art.202 attestations"
    }
    strategy "Legal Strategy" {
        desc: "Action plans, roadmaps, damages matrices"
    }
    actors "Actor Profiles" {
        desc: "Individual profiles, role analyses"
    }
    statute_of_limitations "Statute of Limitations" {
        desc: "Limitation periods, article 2226 Civil Code"
    }
    lawyer_liability "Lawyer Liability" {
        desc: "Poinat professional misconduct, bar complaint"
    }
}

@taxonomy category {
    label: "Document Category"
    facet: true

    legal "Legal" { icon: "balance-scale", color: "#C55A11" }
    technical "Technical" { icon: "cog", color: "#002060" }
    financial "Financial" { icon: "money", color: "#00B050" }
    evidence "Evidence" { icon: "clipboard", color: "#70AD47" }
    synthesis "Synthesis" { icon: "chart-bar", color: "#FF6B6B" }
    strategy "Strategy" { icon: "bullseye", color: "#1F4E79" }
    media "Media" { icon: "newspaper", color: "#FFC000" }
    actors "Actors" { icon: "user", color: "#4ECDC4" }
    med_poinat "MED & Poinat" { icon: "user-tie", color: "#7030A0" }
    medical "Medical" { icon: "hospital", color: "#A6A6A6" }
    personnel "Personnel" { icon: "users", color: "#E7E6E6" }
    archive "Archives" { icon: "archive", color: "#595959" }
}
```

**Semantics:**
- `taxonomy_id` must be unique across all taxonomies
- Nesting creates parent-child hierarchy (unlimited depth)
- `facet: true` makes this taxonomy available as a search facet
- Searching a parent node includes all descendants: `theme:fraud` matches `fraud_tech` and `financial`
- Node properties (`color`, `icon`, `desc`) are free-form metadata
- Node IDs must be unique within the taxonomy

---

### 1.6 Auto-Classification Rules (`@rule`)

Define regex-based rules that automatically classify documents during indexing.

**Syntax:**
```
@rule <rule_id> {
    when content matches "<regex_pattern>"
    then assign <taxonomy_id>: <node_id>
    confidence: <float 0.0-1.0>
}

@rule <rule_id> {
    when content matches "<regex_pattern>"
    then extract <entity_type_id>
}
```

**Examples:**
```dsl
# Assign to taxonomy nodes based on content patterns

@rule auto_track_criminal {
    when content matches "plainte|escroquerie|abus de confiance|p[ée]nal|citation directe"
    then assign track: voie_a
    confidence: 0.7
}

@rule auto_track_labor {
    when content matches "prud.hommes|licenciement|syntec|harc[èe]lement moral|3\\.3"
    then assign track: voie_b
    confidence: 0.8
}

@rule auto_track_lawyer {
    when content matches "poinat|responsabilit[ée] professionnelle|b[âa]tonnier"
    then assign track: voie_c
    confidence: 0.8
}

@rule auto_track_cnil {
    when content matches "CNIL|RGPD|donn[ée]es personnelles|protection des donn[ée]es"
    then assign track: cnil_rgpd
    confidence: 0.8
}

@rule auto_theme_fraud_tech {
    when content matches "bing|repackag|faux moteur|DINUM|audit.+code|sonarqube"
    then assign theme: fraud_tech
    confidence: 0.7
}

@rule auto_theme_financial {
    when content matches "BPI|CDC|caisse des d[ée]p[ôo]ts|haut.+salair|fonds publics"
    then assign theme: financial
    confidence: 0.7
}

@rule auto_theme_whistleblower {
    when content matches "lanceur.d.alerte|repr[ée]sailles|SLAPP|sapin.II"
    then assign theme: whistleblower
    confidence: 0.8
}

# Extract structured entities from content patterns

@rule auto_extract_legal_ref {
    when content matches "article\\s+(L\\.)?\\d[\\d\\-\\.]*"
    then extract legal_reference
}

@rule auto_extract_amount {
    when content matches "\\d[\\d\\s]*[.,]\\d{2}\\s*[€$]|\\d+[\\d\\s]*\\s*(euros|EUR)"
    then extract monetary_amount
}

@rule auto_extract_date {
    when content matches "\\d{1,2}[/\\-]\\d{1,2}[/\\-]\\d{2,4}|\\d{4}[/\\-]\\d{2}[/\\-]\\d{2}"
    then extract date_event
}
```

**Semantics:**
- **`assign` rules**: When the regex matches document content, assign the document to the specified taxonomy node with the given confidence score
- **`extract` rules**: When the regex matches, extract the matched text and create an entity instance of the specified type
- Patterns use standard regex syntax (case-insensitive matching by default)
- `confidence` (0.0-1.0) indicates how likely the classification is correct; higher values override lower ones
- Multiple rules can match the same document
- Rules are evaluated in order; first match with highest confidence wins for conflicting assignments

---

## 2. Query DSL

The query DSL is used in search requests (`GET /api/search?q=...`).

### 2.1 Full-text Search

| Syntax | Description | Example |
|--------|-------------|---------|
| `term` | Single term, searches across all text fields | `fraude` |
| `term1 term2` | Multiple terms (implicit OR) | `fraude technique` |
| `"exact phrase"` | Exact phrase match | `"passage en force bing"` |
| `term~N` | Fuzzy match with Levenshtein distance N | `fraude~2` |
| `prefix*` | Wildcard prefix match | `fraud*` |
| `"phrase"~N` | Proximity: terms within N words of each other | `"audit DINUM"~5` |

### 2.2 Field Filters

Generic field filters using `field:value` syntax.

| Filter | Description | Example |
|--------|-------------|---------|
| `type:ext` | File type | `type:docx` |
| `type:ext1,ext2` | Multiple types (OR) | `type:docx,xlsx,pdf` |
| `tag:value` | Tag match | `tag:latest` |
| `tag:v1,v2` | Multiple tags (OR) | `tag:latest,v2` |
| `date:>YYYY-MM-DD` | After date | `date:>2025-01-01` |
| `date:<YYYY-MM-DD` | Before date | `date:<2026-01-01` |
| `date:FROM..TO` | Date range (inclusive) | `date:2025-01..2025-06` |
| `size:>Nkb` | Minimum size | `size:>100kb` |
| `size:<Nmb` | Maximum size | `size:<5mb` |
| `category:id` | Document category | `category:legal` |

### 2.3 Ontology Filters

Query using the entity graph defined in the ontology.

| Filter | Description | Example |
|--------|-------------|---------|
| `entity:id` | Documents linked to a specific entity | `entity:erard` |
| `entity.type:type_id` | Documents linked to any entity of this type | `entity.type:person` |
| `entity.role:role` | Documents linked to entities with this role | `entity.role:plaintiff` |
| `entity.alias:text` | Match entity by alias text | `entity.alias:serard` |

### 2.4 Taxonomy Filters

Query using taxonomy hierarchies. **Parent nodes include all descendants.**

| Filter | Description | Example |
|--------|-------------|---------|
| `track:node_id` | Legal track | `track:voie_a` |
| `theme:node_id` | Theme (includes children) | `theme:fraud` → matches `fraud_tech` + `financial` |
| `theme:child_id` | Specific theme leaf | `theme:fraud_tech` |
| `category:node_id` | Document category | `category:legal` |

### 2.5 Boolean Operators

| Operator | Behavior | Example |
|----------|----------|---------|
| `AND` | Both clauses must match | `erard AND leandri` |
| `OR` | Either clause must match | `fraude OR escroquerie` |
| `NOT` | Exclude matches | `erard NOT poinat` |
| `(...)` | Grouping for precedence | `(fraude OR escroquerie) AND entity:erard` |

**Precedence:** `NOT` > `AND` > `OR` (override with parentheses).

### 2.6 Symbolic Link Queries

Query the document relationship graph.

| Filter | Description | Example |
|--------|-------------|---------|
| `linked:doc_id` | Documents linked to this document | `linked:analyse_audit_dinum` |
| `link.type:type` | Filter by link type | `link.type:shared_entity` |
| `link.via:entity_id` | Linked through a specific entity | `link.via:erard` |
| `link.strength:>N` | Filter by link strength (0.0-1.0) | `link.strength:>0.5` |

**Link types:** `mentions`, `cites`, `shared_entity`, `shared_theme`, `shared_track`, `co_occurrence`, `temporal_proximity`

### 2.7 Graph Traversal

Navigate the entity/document graph.

| Filter | Description | Example |
|--------|-------------|---------|
| `path:A->B` | Documents on the path between two entities | `path:erard->leandri` |
| `neighbors:doc_id` | Direct neighbors of a document | `neighbors:analyse_audit_dinum` |
| `neighbors:id depth:N` | Neighbors within N hops | `neighbors:analyse_audit_dinum depth:2` |

### 2.8 Facets & Aggregation

Request faceted counts alongside search results.

| Filter | Description | Example |
|--------|-------------|---------|
| `facet:field` | Return facet counts for a field | `facet:entity` |
| `facet:f1,f2` | Multiple facets | `facet:track,theme` |
| `facet:field top:N` | Limit to top N values | `facet:entity top:10` |
| `count:field` | Count documents per field value | `count:entity` |
| `timeline:entity:id` | Temporal distribution for an entity | `timeline:entity:erard` |

---

## 3. Formal Grammar

### 3.1 Schema Grammar (EBNF)

```ebnf
schema_file     = { comment | type_def | entity_def | relation_type_def
                  | relation_def | taxonomy_def | rule_def } ;

comment         = "#" { any_char } newline ;

(* Entity Types *)
type_def        = "@type" identifier "{" { property_def } "}" ;
property_def    = identifier ":" type_expr ;
type_expr       = "string" | "number" | "date" | "bool"
                | "enum" "[" identifier { "," identifier } "]"
                | type_expr "[]" ;

(* Entity Instances *)
entity_def      = "@entity" identifier ":" identifier "{" { assignment } "}" ;
assignment      = identifier ":" value ;
value           = string_literal | number_literal | identifier | array_literal ;
array_literal   = "[" value { "," value } "]" ;
string_literal  = '"' { any_char } '"' ;

(* Relation Types *)
relation_type_def = "@relation.type" identifier "{" { rel_type_prop } "}" ;
rel_type_prop     = "from:" type_ref
                  | "to:" type_ref
                  | "properties:" "[" identifier { "," identifier } "]"
                  | "inverse:" identifier
                  | "auto:" bool_literal
                  | "symmetric:" bool_literal ;
type_ref          = identifier | "[" identifier { "," identifier } "]" ;

(* Relation Instances *)
relation_def    = "@relation" identifier "-[" identifier "]->" identifier
                  "{" { assignment } "}" ;

(* Taxonomies *)
taxonomy_def    = "@taxonomy" identifier "{" { tax_prop | tax_node } "}" ;
tax_prop        = "label:" string_literal
                | "facet:" bool_literal ;
tax_node        = identifier string_literal "{" { tax_prop | tax_node } "}" ;

(* Rules *)
rule_def        = "@rule" identifier "{" rule_body "}" ;
rule_body       = "when" "content" "matches" string_literal
                  ( "then" "assign" identifier ":" identifier "confidence:" number_literal
                  | "then" "extract" identifier ) ;

(* Terminals *)
identifier      = letter { letter | digit | "_" | "." } ;
bool_literal    = "true" | "false" ;
number_literal  = digit { digit } [ "." digit { digit } ] ;
```

### 3.2 Query Grammar (EBNF)

```ebnf
query           = clause { boolean_op clause } ;
clause          = "(" query ")"
                | not_clause
                | field_filter
                | phrase
                | fuzzy_term
                | wildcard_term
                | term ;

not_clause      = "NOT" clause ;

boolean_op      = "AND" | "OR" ;

field_filter    = field_name ":" field_value ;
field_name      = "entity" | "entity.type" | "entity.role" | "entity.alias"
                | "track" | "theme" | "category"
                | "type" | "tag" | "date" | "size"
                | "linked" | "link.type" | "link.via" | "link.strength"
                | "path" | "neighbors" | "facet" | "count" | "timeline"
                | "depth" | "top" ;

field_value     = comparison_value
                | range_value
                | csv_values
                | path_value
                | value ;

comparison_value = ( ">" | "<" | ">=" | "<=" ) value ;
range_value     = value ".." value ;
csv_values      = value "," value { "," value } ;
path_value      = identifier "->" identifier ;

phrase          = '"' { any_char } '"' [ "~" integer ] ;
fuzzy_term      = word "~" [ integer ] ;
wildcard_term   = word "*" ;
term            = word ;
word            = { letter | digit | "_" | "-" } ;
```

---

## 4. Data Types

| Type | Schema Syntax | Query Syntax | Examples |
|------|---------------|-------------|----------|
| String | `"text"` | `"text"` or bare word | `"Stephane Erard"`, `erard` |
| Number | `42`, `3.14` | `42`, `3.14` | `confidence: 0.7` |
| Date | `"YYYY-MM-DD"` | `YYYY-MM-DD` | `date:>2025-01-01` |
| Boolean | `true`, `false` | - | `auto: true` |
| Enum | bare identifier | bare identifier | `role: plaintiff` |
| Array | `[v1, v2, ...]` | `v1,v2` (CSV) | `type:docx,xlsx,pdf` |
| Identifier | letter + alphanum/underscore | same | `entity:erard`, `theme:fraud_tech` |
| Regex | `"pattern"` (in @rule) | - | `"plainte\|escroquerie"` |
| Size | - | `Nkb`, `Nmb` | `size:>100kb` |
| Range | - | `A..B` | `date:2025-01..2025-06` |
| Path | - | `A->B` | `path:erard->leandri` |

---

## 5. Complete Example (`ontology.dsl`)

```dsl
# ═══════════════════════════════════════════════════════════════
# Domain Ontology for Erard v. Qwant
# ═══════════════════════════════════════════════════════════════

# ─── TYPES ────────────────────────────────────────────────────

@type person {
    name: string
    role: enum [plaintiff, defendant, witness, accomplice, executive, journalist, lawyer, politician, involved]
    aliases: string[]
}

@type organization {
    name: string
    role: enum [defendant, acquired_company, partner]
    aliases: string[]
}

@type institution {
    name: string
    role: enum [authority, investor, regulator]
    aliases: string[]
}

@type technology {
    name: string
    role: enum [search_engine, tool, platform]
    aliases: string[]
}

@type legal_reference {
    code: string
    article: string
    description: string
}

@type monetary_amount {
    value: number
    currency: string
    context: string
}

# ─── ENTITIES ─────────────────────────────────────────────────

@entity erard : person {
    name: "Stephane Erard"
    role: plaintiff
    aliases: ["erard", "serard", "stephane", "s. erard", "stephane erard"]
}

@entity qwant : organization {
    name: "Qwant SAS"
    role: defendant
    aliases: ["qwant", "qwant sas", "qwant.com"]
}

@entity leandri : person {
    name: "Eric Leandri"
    role: executive
    aliases: ["leandri", "eric leandri", "e. leandri"]
}

@entity vignaux : person {
    name: "Pierre Vignaux"
    role: accomplice
    aliases: ["vignaux", "pierre vignaux", "p. vignaux"]
}

@entity poinat : person {
    name: "Me Poinat"
    role: lawyer
    aliases: ["poinat", "me poinat", "maitre poinat"]
}

@entity cassar : person {
    name: "Jonathan Cassar"
    role: witness
    aliases: ["cassar", "jonathan cassar", "jcc"]
}

@entity decaux : person {
    name: "Thomas Decaux"
    role: involved
    aliases: ["decaux", "thomas decaux"]
}

@entity bourrelly : person {
    name: "Bourrelly"
    role: involved
    aliases: ["bourrelly"]
}

@entity champeau : person {
    name: "G. Champeau"
    role: journalist
    aliases: ["champeau", "g. champeau", "guillaume champeau"]
}

@entity mathieu : person {
    name: "Eric Mathieu"
    role: witness
    aliases: ["mathieu", "eric mathieu"]
}

@entity cnil : institution {
    name: "CNIL"
    role: authority
    aliases: ["cnil", "commission nationale informatique et libertes"]
}

@entity dinum : institution {
    name: "DINUM"
    role: authority
    aliases: ["dinum", "direction interministerielle du numerique"]
}

@entity bpi : institution {
    name: "BPI/CDC"
    role: investor
    aliases: ["bpi", "cdc", "caisse des depots", "bpifrance"]
}

@entity xilopix : organization {
    name: "Xilopix/Xaphir"
    role: acquired_company
    aliases: ["xilopix", "xaphir"]
}

@entity bing : technology {
    name: "Microsoft Bing"
    role: search_engine
    aliases: ["bing", "microsoft bing", "bing api"]
}

@entity macron : person {
    name: "Emmanuel Macron"
    role: politician
    aliases: ["macron", "emmanuel macron"]
}

# ─── RELATION TYPES ──────────────────────────────────────────

@relation.type employs {
    from: organization
    to: person
    properties: [start_date, end_date, position]
    inverse: employed_by
}

@relation.type accuses {
    from: person
    to: [person, organization]
    properties: [reason, date, severity]
    inverse: accused_by
}

@relation.type directs {
    from: person
    to: organization
    properties: [start_date, end_date, title]
    inverse: directed_by
}

@relation.type represents {
    from: person
    to: person
    properties: [mandate_type, start_date]
    inverse: represented_by
}

@relation.type invests_in {
    from: institution
    to: organization
    properties: [amount, date, investment_type]
    inverse: funded_by
}

@relation.type acquired {
    from: organization
    to: organization
    properties: [date, amount, conditions]
    inverse: acquired_by
}

@relation.type uses {
    from: organization
    to: technology
    properties: [start_date, context]
}

@relation.type regulates {
    from: institution
    to: [person, organization]
    properties: [procedure_type, case_number]
    inverse: regulated_by
}

@relation.type mentions {
    from: document
    to: [person, organization, institution, technology, legal_reference]
    auto: true
    properties: [count, positions]
}

@relation.type cites {
    from: document
    to: legal_reference
    auto: true
    properties: [context]
}

@relation.type shared_context {
    from: document
    to: document
    auto: true
    symmetric: true
    properties: [via_entities, via_themes, strength]
}

# ─── RELATION INSTANCES ──────────────────────────────────────

@relation erard -[employed_by]-> qwant {
    start_date: "2017-01"
    end_date: "2019-07"
    position: "Senior R&D Engineer"
}

@relation leandri -[directs]-> qwant {
    start_date: "2013"
    end_date: "2020"
    title: "CEO co-founder"
}

@relation poinat -[represents]-> erard {
    mandate_type: "labor_court"
    start_date: "2019"
}

@relation bpi -[invests_in]-> qwant {
    amount: "25000000"
    investment_type: "public_funds"
}

@relation qwant -[acquired]-> xilopix {
    date: "2017"
    conditions: "technology absorption"
}

@relation qwant -[uses]-> bing {
    start_date: "2019"
    context: "repackaged search results as proprietary"
}

@relation erard -[accuses]-> qwant {
    reason: "technical_fraud"
    severity: "high"
}

@relation erard -[accuses]-> leandri {
    reason: "moral_harassment"
    severity: "high"
}

@relation erard -[accuses]-> vignaux {
    reason: "complicity_bing_forced_migration"
    severity: "medium"
}

@relation erard -[accuses]-> poinat {
    reason: "professional_misconduct"
    severity: "medium"
}

@relation cnil -[regulates]-> qwant {
    procedure_type: "gdpr_complaint"
    case_number: "19005268"
}

# ─── TAXONOMIES ───────────────────────────────────────────────

@taxonomy track {
    label: "Legal Track"
    facet: true

    voie_a "Criminal Track" {
        color: "#E74C3C"
        desc: "Criminal complaints, fraud, breach of trust"
    }
    voie_b "Labor Track" {
        color: "#3498DB"
        desc: "Unfair dismissal, harassment, Syntec 3.3"
    }
    voie_c "Lawyer Track" {
        color: "#9B59B6"
        desc: "Professional liability Me Poinat"
    }
    cnil_rgpd "CNIL/GDPR" {
        color: "#F39C12"
        desc: "Data protection, CNIL complaint"
    }
    media "Media Strategy" {
        color: "#1ABC9C"
        desc: "Press releases, press kits, op-eds"
    }
}

@taxonomy theme {
    label: "Theme"
    facet: true

    fraud "Fraud" {
        fraud_tech "Technical Fraud" {
            desc: "Bing repackaging, fake engine, DINUM audit"
        }
        financial "Financial Misappropriation" {
            desc: "BPI/CDC, salaries, public funds fraud"
        }
    }
    labor_law "Labor Law" {
        employment "Employment" {
            desc: "Dismissal, Syntec 3.3, reclassification"
        }
        harassment "Harassment" {
            desc: "Moral harassment, pressure, SLAPP"
        }
    }
    whistleblower "Whistleblower" {
        desc: "Protection, retaliation, Sapin II"
    }
    gdpr "GDPR/Data" {
        desc: "GDPR violations, CNIL complaint"
    }
    evidence "Evidence & Forensics" {
        desc: "Git analysis, screenshots, attestations"
    }
    strategy "Legal Strategy" {
        desc: "Action plans, damages matrices"
    }
    actors "Actor Profiles" {
        desc: "Individual profiles, role analyses"
    }
    statute_of_limitations "Statute of Limitations" {
        desc: "Limitation periods, art. 2226"
    }
    lawyer_liability "Lawyer Liability" {
        desc: "Poinat misconduct, bar complaint"
    }
}

@taxonomy category {
    label: "Document Category"
    facet: true

    legal "Legal" { icon: "balance-scale", color: "#C55A11" }
    technical "Technical" { icon: "cog", color: "#002060" }
    financial "Financial" { icon: "money", color: "#00B050" }
    evidence "Evidence" { icon: "clipboard", color: "#70AD47" }
    synthesis "Synthesis" { icon: "chart-bar", color: "#FF6B6B" }
    strategy "Strategy" { icon: "bullseye", color: "#1F4E79" }
    media "Media" { icon: "newspaper", color: "#FFC000" }
    actors "Actors" { icon: "user", color: "#4ECDC4" }
    med_poinat "MED & Poinat" { icon: "user-tie", color: "#7030A0" }
    medical "Medical" { icon: "hospital", color: "#A6A6A6" }
    personnel "Personnel" { icon: "users", color: "#E7E6E6" }
    archive "Archives" { icon: "archive", color: "#595959" }
}

# ─── RULES ────────────────────────────────────────────────────

@rule auto_track_criminal {
    when content matches "plainte|escroquerie|abus de confiance|p[ée]nal|citation directe"
    then assign track: voie_a
    confidence: 0.7
}

@rule auto_track_labor {
    when content matches "prud.hommes|licenciement|syntec|harc[èe]lement moral|3\\.3"
    then assign track: voie_b
    confidence: 0.8
}

@rule auto_track_lawyer {
    when content matches "poinat|responsabilit[ée] professionnelle|b[âa]tonnier"
    then assign track: voie_c
    confidence: 0.8
}

@rule auto_track_cnil {
    when content matches "CNIL|RGPD|donn[ée]es personnelles"
    then assign track: cnil_rgpd
    confidence: 0.8
}

@rule auto_theme_fraud_tech {
    when content matches "bing|repackag|faux moteur|DINUM|sonarqube"
    then assign theme: fraud_tech
    confidence: 0.7
}

@rule auto_theme_financial {
    when content matches "BPI|CDC|caisse des d[ée]p[ôo]ts|fonds publics"
    then assign theme: financial
    confidence: 0.7
}

@rule auto_theme_whistleblower {
    when content matches "lanceur.d.alerte|repr[ée]sailles|SLAPP|sapin.II"
    then assign theme: whistleblower
    confidence: 0.8
}

@rule auto_theme_harassment {
    when content matches "harc[èe]lement|intimidation|pression|burn.?out"
    then assign theme: harassment
    confidence: 0.7
}

@rule auto_extract_legal_ref {
    when content matches "article\\s+(L\\.)?\\d[\\d\\-\\.]*"
    then extract legal_reference
}

@rule auto_extract_amount {
    when content matches "\\d[\\d\\s]*[.,]\\d{2}\\s*[€$]|\\d+[\\d\\s]*\\s*(euros|EUR)"
    then extract monetary_amount
}

@rule auto_extract_date {
    when content matches "\\d{1,2}[/\\-]\\d{1,2}[/\\-]\\d{2,4}|\\d{4}[/\\-]\\d{2}[/\\-]\\d{2}"
    then extract date_event
}
```
