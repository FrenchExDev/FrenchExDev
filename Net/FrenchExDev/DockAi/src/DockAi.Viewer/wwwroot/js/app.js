import { ApiClient } from './api-client.js';
import { Sidebar } from './components/sidebar.js';
import { SearchView } from './views/search-view.js';
import { DocumentView } from './views/document-view.js';
import { GraphView } from './views/graph-view.js';
import { TimelineView } from './views/timeline-view.js';
import { PreviewWindowManager } from './components/preview-window.js';

class App {
    constructor() {
        this.api = new ApiClient('/proxy');
        this.currentView = 'documents';
        this.currentDocId = null;

        // Views
        this.sidebar = new Sidebar(
            document.getElementById('sidebar'),
            this.api,
            (docId, docName) => this.openDocument(docId, docName)
        );

        this.docView = new DocumentView(
            document.getElementById('view-documents'),
            this.api
        );

        this.graphView = new GraphView(
            document.getElementById('view-graph'),
            this.api
        );

        this.timelineView = new TimelineView(
            document.getElementById('view-timeline'),
            this.api
        );

        this.searchView = new SearchView(
            document.getElementById('search-input'),
            document.getElementById('facets'),
            this.api,
            (result) => this.onSearchResults(result)
        );

        this.previewManager = new PreviewWindowManager(
            document.getElementById('preview-layer'),
            this.api
        );

        this.initTabs();
        this.initActions();
        this.loadSidebar();
    }

    initTabs() {
        document.querySelectorAll('.view-tab').forEach(tab => {
            tab.onclick = () => this.switchView(tab.dataset.view);
        });
    }

    initActions() {
        document.getElementById('btn-index').onclick = async () => {
            const btn = document.getElementById('btn-index');
            btn.textContent = 'Indexing...';
            btn.disabled = true;
            try {
                await this.api.triggerIndex();
                await this.loadSidebar();
            } catch (e) {
                console.error('Index failed:', e);
            }
            btn.textContent = 'Index';
            btn.disabled = false;
        };

        // Keyboard shortcut: Ctrl+K to focus search
        document.addEventListener('keydown', (e) => {
            if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                e.preventDefault();
                document.getElementById('search-input').focus();
            }
        });
    }

    async loadSidebar() {
        await this.sidebar.load();
    }

    switchView(view) {
        this.currentView = view;

        document.querySelectorAll('.view-tab').forEach(t =>
            t.classList.toggle('active', t.dataset.view === view)
        );

        const views = { documents: this.docView, graph: this.graphView, timeline: this.timelineView };
        for (const [name, v] of Object.entries(views)) {
            const el = document.getElementById(`view-${name}`);
            if (name === view) {
                el.style.display = 'block';
            } else {
                el.style.display = 'none';
                if (v.hide) v.hide();
            }
        }

        if (view === 'graph') {
            this.graphView.show((docId) => this.openDocument(docId));
        } else if (view === 'timeline') {
            this.timelineView.show((docId) => this.openDocument(docId));
        }
    }

    async openDocument(docId, docName) {
        this.currentDocId = docId;

        if (this.currentView === 'documents') {
            await this.docView.show(docId);
            this.sidebar.setActive(docId);
        } else {
            // Open in preview window from graph/timeline views
            await this.previewManager.open(docId, docName || docId);
        }
    }

    onSearchResults(result) {
        const docs = result.results || [];
        if (docs.length === 0) {
            document.getElementById('view-documents').innerHTML =
                '<div class="doc-placeholder">No results found</div>';
            return;
        }

        // Update sidebar with search results
        this.sidebar.renderResults(docs);

        // Show first result
        if (docs.length > 0) {
            this.switchView('documents');
            this.openDocument(docs[0].id, docs[0].name);
        }
    }
}

// Boot
document.addEventListener('DOMContentLoaded', () => new App());
