/* eslint-disable no-useless-constructor */
/* global i18n */

import { repeat } from 'lit-html/directives/repeat.js';
import { LitMvvmElement, html, css } from '@kdsoft/lit-mvvm';
import '@kdsoft/lit-mvvm-components';
import dialogPolyfill from 'dialog-polyfill';
import checkboxStyles from '../styles/kds-checkbox-styles.js';
import fontAwesomeStyles from '../styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import gridStyles from '../styles/kds-grid-styles.js';
import dialogStyles from '../styles/dialog-polyfill-styles.js';
import '../components/etw-checklist.js';
import './etw-app-side-bar.js';
import './provider-config.js';
import './filter-edit.js';
import './event-sink-config.js';
import './live-view-config.js';
import * as utils from '../js/utils.js';

const dialogClass = utils.html5DialogSupported ? '' : 'fixed';

class EtwAgent extends LitMvvmElement {
  constructor() {
    super();
    // setting model property here because we cannot reliable set it from a non-lit-html rendered HTML page
    // we must assign the model *after* the scheduler, or assign it externally
    // this.model = new EtwAppModel(); --
  }

  //#region Providers

  _addProviderClick(e, agentState) {
    agentState.addProvider('<New Provider>', 0);
  }

  _deleteProviderClick(e, agentState) {
    const provider = e.detail.model;
    agentState.removeProvider(provider.name);
  }

  //#endregion

  //#region Processing

  _processingFieldChange(e, agentState) {
    e.stopPropagation();
    agentState.processingModel[e.target.name] = utils.getFieldValue(e.target);
  }

  //#endregion

  //#region Event Sinks

  _addEventSinkClick() {
    const dlg = this.renderRoot.getElementById('dlg-add-event-sink');
    dlg.querySelector('form').reset();
    dlg.querySelector('#sinktype-list').model.unselectAll();
    dlg.showModal();
  }

  _okAddEventSink(e, agentState) {
    e.preventDefault();
    e.stopImmediatePropagation();
    const form = e.currentTarget.closest('form');
    const sinkname = form.querySelector('#sink-name');
    const sinklist = form.querySelector('#sinktype-list');
    const selectedSinkInfo = sinklist.model.firstSelectedEntry?.item;
    sinklist._internals.setValidity({ valueMissing: !selectedSinkInfo }, 'Sink type must be selected.');
    if (!form.reportValidity()) return;

    this.model.addEventSink(agentState, sinkname.value, selectedSinkInfo);

    form.parentElement.close();
  }

  _cancelAddEventSink(e) {
    const dlg = e.currentTarget.closest('dialog');
    dlg.close();
  }

  _deleteEventSinkClick(e, agentState) {
    const model = e.currentTarget.model;
    this.model.deleteEventSink(agentState, model.profile.name);
  }

  _updateEventSinks(e, agentState) {
    const configForms = this.renderRoot.querySelectorAll('event-sink-config');
    let isValid = true;
    configForms.forEach(frm => {
      isValid = isValid && frm.isValid();
    });
    if (isValid) {
      this.model.updateEventSinks(agentState);
    }
  }

  _exportEventSinks(e, agentState) {
    const profiles = Object.entries(agentState.eventSinks).map(es => es[1].profile);
    const profileString = JSON.stringify(profiles, null, 2);
    const profileURL = `data:text/plain,${profileString}`;

    const a = document.createElement('a');
    try {
      a.style.display = 'none';
      a.href = profileURL;
      a.download = `${agentState.id}_eventSinks.json`;
      document.body.appendChild(a);
      a.click();
    } finally {
      document.body.removeChild(a);
    }
  }

  _importEventSinksDialog(e) {
    const fileDlg = e.currentTarget.parentElement.querySelector('input[type="file"]');
    // reset value so that @change event fires reliably
    fileDlg.value = null;
    fileDlg.click();
  }

  _importEventSinks(e, agentState) {
    const selectedFile = e.currentTarget.files[0];
    if (!selectedFile) return;

    selectedFile.text().then(txt => {
      const profiles = JSON.parse(txt);
      const eventSinks = {};
      for (const profile of profiles) {
        eventSinks[profile.name] = { profile };
      }
      agentState.eventSinks = eventSinks;
    });
  }

  //#endregion

  //#region overrides

  /* eslint-disable indent, no-else-return */

  _addFormHandlers(frm) {
    frm.addEventListener('kdsoft-done', this._formDoneHandler);
  }

  _removeFormHandlers(frm) {
    frm.removeEventListener('kdsoft-done', this._formDoneHandler);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    const eventSinkForm = this.renderRoot.getElementById('event-sink');
    if (eventSinkForm) this._removeFormHandlers(eventSinkForm);
  }

  shouldRender() {
    return !!(this.model.getActiveEntry());
  }

  // first event where model is available
  beforeFirstRender() {
    this.model._kds_tabs = { items: ['Providers', 'Event Sinks', 'Live View', 'Filter'] };
    this.model._kds_activeTab = 0;
  }

  // called at most once every time after connectedCallback was executed
  static get styles() {
    return [
      tailwindStyles,
      checkboxStyles,
      fontAwesomeStyles,
      gridStyles,
      utils.html5DialogSupported ? dialogStyles : css``,
      css`
        :host {
          display: block;
          position: relative;
        }

        kds-tab-container {
          height: 100%;
          --top-row: min-content;
          --left-col: 0;
          --right-col: 0;
          --main-row: auto;
          --bottom-row: 0;
        }

        kds-tab-container > form {
          position: relative;
          display:flex;
          width: 100%;
          break-inside: avoid;
          margin: 0;
          padding: 0.75rem;
        }

        kds-tab-container::part(header) {
          border-bottom: 2px darkgrey solid;
        }

        button[slot="tabs"][active] {
          border-bottom: 2px white solid;
          margin-bottom: -2px;
          border-left: 2px darkgrey solid;
          border-right: 2px darkgrey solid;
          font-weight: bold;
          z-index: 2;
        }

        button[slot="tabs"][first] {
          border-left: 0;
        }

        label {
          font-weight: bolder;
          color: #718096;
        }

        input {
          border-width: 1px;
        }

        .form-header {
          position:absolute;
          left:0;
          right:0;
          top:0;
        }

        .form-footer {
          position:absolute;
          left:0;
          right:0;
          bottom:0;
        }

        .form-body {
          position:absolute;
          left: 0;
          right: 0;
          top: 0;
          bottom: 2em;
        }

        .providers, .eventSinks {
          display: flex;
          writing-mode: horizontal-tb;
          flex-wrap: wrap;
          align-content: flex-start;
          overflow-y: auto;
          gap: 0.5rem;
        }

        #sinktype-list {
          min-width: 12rem;
        }

        #providers, #event-sinks, #live-view, #processing {
          margin: 1rem;
        }

        #live-view {
          --max-scroll-height: 60vh;
        }

        #processing {
          width: 100%;
        }

        #dlg-add-event-sink form {
          display: grid;
          grid-template-columns: auto auto;
          background: rgba(255,255,255,0.3);
          row-gap: 5px;
          column-gap: 10px;
        }

        #dlg-add-event-sink form > h3 {
          grid-column: 1 / -1;
          justify-self: center;
          margin-bottom: .5em;
        }
      `
    ];
  }

  firstRendered() {
    const eventSinkDlg = this.renderRoot.getElementById('dlg-add-event-sink');
    if (!utils.html5DialogSupported) {
      dialogPolyfill.registerDialog(eventSinkDlg);
    }
  }

  render() {
    const activeEntry = this.model.getActiveEntry();
    const activeAgentState = this.model.activeAgentState;
    const processingModel = activeAgentState.processingModel;
    return html`
      <kds-tab-container id="main">
        ${this.model._kds_tabs.items.map((tab, index) => {
          const active = index === this.model._kds_activeTab;
          let modified = false;
          switch (index) {
            case 0:
              modified = this.model.getProvidersModified(activeEntry);
              break;
            case 1:
              modified = this.model.getEventSinksModified(activeEntry);
              break;
            case 2:
              modified = this.model.getLiveViewOptionsModified(activeEntry);
              break;
            case 3:
              modified = this.model.getProcessingModified(activeEntry);
              break;
            default:
              break;
          }
          const modifiedClass = modified ? 'italic text-red-500' : '';
          return html`
            <button type="button" slot="tabs" class="px-2 py-1 bg-white" ?active=${active} ?first=${index === 0}
                @click=${() => { this.model._kds_activeTab = index; }}
            >
              <span class="${modifiedClass}">${tab}</span>
            </button>`;
          }
        )}

        <form id="providers" class="border" style="${this.model._kds_activeTab === 0 ? '' : 'display:none'}">
          <div class="form-body providers border">
            ${activeAgentState.enabledProviders.map(provider => html`
              <provider-config
                .model=${provider}
                @delete=${e => this._deleteProviderClick(e, activeAgentState)}>
              </provider-config>
            `)}
          </div>
          <div class="form-footer flex flex-wrap mt-2 bt-1">
            <button type="button" class="py-1 px-2 ml-auto" @click=${e => this._addProviderClick(e, activeAgentState)}>
              <i class="fas fa-lg fa-plus text-gray-500"></i>
            </button>
            <button type="button" class="py-1 px-2 ml-4" @click=${() => this.model.applyProviders(activeAgentState)} title="Apply">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.resetProviders(activeEntry)} title="Reset to Current">
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form>

        <form id="event-sinks" class="border" style="${this.model._kds_activeTab === 1 ? '' : 'display:none'}">
          <div class="form-body eventSinks border">
            ${repeat(
              Object.entries(activeAgentState.eventSinks),
              entry => entry[0],
              entry => html`
                <event-sink-config class="bg-gray-300"
                  .model=${entry[1]}
                  @delete=${e => this._deleteEventSinkClick(e, activeAgentState)}>
                </event-sink-config>
              `
            )}
          </div>
          <div id="ok-cancel-buttons" class="form-footer flex flex-wrap mt-2 bt-1">
            <input type="file" accept=".json"
              @change=${e => this._importEventSinks(e, activeAgentState)}
              hidden />
            <button type="button" class="mr-1 text-gray-600" @click=${e => this._importEventSinksDialog(e)} title="Import">
              <i class="fas fa-lg fa-file-import"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${e => this._exportEventSinks(e, activeAgentState)} title="Export">
              <i class="fas fa-lg fa-file-export text-gray-600"></i>
            </button>
            <button type="button" class="py-1 px-2 ml-auto" @click=${this._addEventSinkClick}>
              <i class="fas fa-lg fa-plus text-gray-500"></i>
            </button>
            <button type="button" class="py-1 px-2 ml-4" @click=${e => this._updateEventSinks(e, activeAgentState)} title="Apply">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.resetEventSinks(activeEntry)} title="Reset to Current" autofocus>
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form>

        <form id="live-view" class="border" style="${this.model._kds_activeTab === 2 ? '' : 'display:none'}">
          <live-view-config class="form-body"
            .model=${activeAgentState.liveViewConfigModel}
            .changeCallback=${opts => this.model.updateLiveViewOptions(activeEntry, opts)}
          ></live-view-config>
          <div class="form-footer flex flex-wrap mt-2 bt-1">
            <button type="button" class="py-1 px-2 ml-auto" @click=${() => this.model.applyLiveViewOptions(activeAgentState)} title="Apply">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.resetLiveViewOptions(activeEntry)} title="Reset to Current">
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form>

        <form id="processing" class="border" style="${this.model._kds_activeTab === 3 ? '' : 'display:none'}"
           @change=${e => this._processingFieldChange(e, activeAgentState)}>
          <filter-edit id="filterEdit" class="form-body p-2" .model=${processingModel.filter}></filter-edit>
          <div class="form-footer flex flex-wrap mt-2 bt-1">
            <button type="button" class="py-1 px-2" @click=${() => this.model.testFilter(activeAgentState)} title="Test">
              <i class="fas fa-lg fa-vial" style="color:orange"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.clearFilter(activeAgentState)} title="Clear">
              <i class="fas fa-lg fa-ban text-gray-500"></i>
            </button>
            <button type="button" class="py-1 px-2 ml-auto" @click=${() => this.model.applyProcessingOptions(activeAgentState)} title="Apply">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="button" class="py-1 px-2" @click=${() => this.model.resetProcessingOptions(activeEntry)} title="Reset to Current">
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form> 

      </kds-tab-container>

      <dialog id="dlg-add-event-sink" class="${dialogClass}">
        <form name="add-event-sink"
          @submit=${e => this._okAddEventSink(e, activeAgentState)}
          @reset=${this._cancelAddEventSink}
        >
          <h3 class="font-bold">Add Event Sink</h3>
          <label for="sink-name">Name</label>
          <input id="sink-name" name="name" type="text" required />
          <label for="sinktype-list">Sink Type</label>
          <etw-checklist id="sinktype-list" class="text-black"
            .model=${this.model.sinkInfoCheckListModel}
            .itemTemplate=${item => html`<div class="flex w-full"><span class="mr-1">${item.sinkType}</span><span class="ml-auto">(${item.version})</span></div>`}
            checkboxes
            required
            tabindex=-1>
          </etw-checklist>
          <span></span>
          <div class="flex flex-wrap ml-auto mt-2 bt-1">
            <button type="submit" class="py-1 px-2 ml-auto" title="Add">
              <i class="fas fa-lg fa-check text-green-500"></i>
            </button>
            <button type="reset" class="py-1 px-2" title="Cancel">
              <i class="fas fa-lg fa-times text-red-500"></i>
            </button>
          </div>
        </form>
      </dialog>
    `;
  }

  //#endregion
}

window.customElements.define('etw-agent', EtwAgent);
