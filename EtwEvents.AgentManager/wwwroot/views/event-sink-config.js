﻿import { observe, observable, unobserve } from '@nx-js/observer-util';
import { LitMvvmElement, html, nothing, css } from '@kdsoft/lit-mvvm';
import checkboxStyles from '../styles/kds-checkbox-styles.js';
import fontAwesomeStyles from '../styles/fontawesome/css/all-styles.js';
import tailwindStyles from '../styles/tailwind-styles.js';
import * as utils from '../js/utils.js';

function sinkConfigForm(sinkType) {
  switch (sinkType) {
    case 'ElasticSink':
      return 'elastic-sink-config';
    case 'OpenSearchSink':
      return 'opensearch-sink-config';
    case 'gRPCSink':
      return 'grpc-sink-config';
    case 'MongoSink':
      return 'mongo-sink-config';
    case 'RollingFileSink':
      return 'rolling-file-sink-config';
    case 'SeqSink':
      return 'seq-sink-config';
    case 'DataCollectorSink':
      return 'data-collector-sink-config';
    case 'LogsIngestionSink':
      return 'logs-ingestion-sink-config';
    default:
      throw new Error(`No configuration form for '${sinkType}'.`);
  }
}

function sinkConfigModel(sinkType) {
  switch (sinkType) {
    case 'ElasticSink':
      return 'elastic-sink-config-model';
    case 'OpenSearchSink':
      return 'opensearch-sink-config-model';
    case 'gRPCSink':
      return 'grpc-sink-config-model';
    case 'MongoSink':
      return 'mongo-sink-config-model';
    case 'RollingFileSink':
      return 'rolling-file-sink-config-model';
    case 'SeqSink':
      return 'seq-sink-config-model';
    case 'DataCollectorSink':
      return 'data-collector-sink-config-model';
    case 'LogsIngestionSink':
      return 'logs-ingestion-sink-config-model';
    default:
      throw new Error(`No configuration model for '${sinkType}'.`);
  }
}

async function loadSinkDefinitionTemplate(sinkType) {
  // Vite can only analyze the dynamic import if we provide a file extension
  const elementModule = await import(`../eventSinks/${sinkType}/${sinkConfigForm(sinkType)}.js`);
  const configElement = elementModule.default;
  return configElement;
}

async function loadSinkDefinitionModel(sinkType) {
  const modelModule = await import(`../eventSinks/${sinkType}/${sinkConfigModel(sinkType)}.js`);
  const ModelClass = modelModule.default;
  return new ModelClass();
}

class EventSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    // for "nothing" to work we need to render raw(this.sinkTypeTemplateHolder.value)
    this.sinkTypeTemplateHolder = observable({ tag: nothing });
  }

  _deleteClicked(e) {
    // send event to parent to remove from list
    const evt = new CustomEvent('delete', {
      // composed allows bubbling beyond shadow root
      bubbles: true, composed: true, cancelable: true, detail: { model: this.model }
    });
    this.dispatchEvent(evt);
  }

  _fieldChange(e) {
    e.stopPropagation();
    this.model.profile[e.target.name] = utils.getFieldValue(e.target);
  }

  isValid() {
    return this.renderRoot.querySelector('form').reportValidity()
      && this.renderRoot.querySelector('#form-content > *')?.isValid();
  }

  async _loadConfigComponent(model) {
    if (model) {
      try {
        const configFormTemplate = await loadSinkDefinitionTemplate(model.profile.sinkType);
        this.sinkConfigModel = await loadSinkDefinitionModel(model.profile.sinkType);
        // update the sinkConfigModel's credentials and options separately from profile,
        // as otherwise we would just replace the options and credentials properties entirely
        utils.setTargetProperties(this.sinkConfigModel.credentials, model.profile.credentials);
        utils.setTargetProperties(this.sinkConfigModel.options, model.profile.options);
        this.sinkTypeTemplateHolder.tag = configFormTemplate(this.sinkConfigModel);
      } catch (error) {
        // for "nothing" to work we need to render raw(this.sinkTypeTemplateHolder.value)
        this.sinkTypeTemplateHolder.tag = nothing;
        window.etwApp.defaultHandleError(error);
      }
    } else {
      this.sinkTypeTemplateHolder.tag = nothing;
    }
  }

  /* eslint-disable indent, no-else-return */

  disconnectedCallback() {
    if (this.configObserver) {
      unobserve(this.configObserver);
      this.configObserver = null;
    }
    super.disconnectedCallback();
  }

  shouldRender() {
    return !!this.model;
  }

  async beforeFirstRender() {
    await this._loadConfigComponent(this.model);
    this.configObserver = observe(() => {
      this.model.profile.credentials = this.sinkConfigModel.credentials || {};
      this.model.profile.options = this.sinkConfigModel.options || {};
    });
  }

  static get styles() {
    return [
      fontAwesomeStyles,
      tailwindStyles,
      checkboxStyles,
      //appStyles,
      css`
        :host {
          display: block;
        }

        form {
          position: relative;
          display: flex;
          flex-direction: column;
          align-items: stretch;
          justify-content: flex-end;
        }

        #form-header {
          display: grid;
          grid-template-columns: auto auto;
          row-gap: 5px;
          column-gap: 10px;
          margin-bottom: 15px;
          padding-left: 5px;
          background: rgba(255,255,255,0.3);
        }

        #form-header>pre {
          grid-column: 1/-1;
        }

        #form-content {
          position: relative;
          display: flex;
          flex-direction: column;
          flex-grow: 1;
          overflow-y: auto;
          min-height: 200px;
        }

        label {
          font-weight: bolder;
          color: #718096;
        }

        input, textarea {
          border-width: 1px;
        }

        input:invalid {
          border: 2px solid red;
        }

        #common-fields {
          display: grid;
          grid-template-columns: auto auto;
          row-gap: 5px;
          column-gap: 10px;
          margin-bottom: 10px;
        }
      `,
    ];
  }

  render() {
    const profile = this.model.profile;
    const status = this.model.status;
    const borderColor = 'border-transparent';
    const timesClasses = 'text-gray-600 fas fa-lg fa-times';
    const errorClasses = status?.lastError ? 'border-red-500 focus:outline-none focus:border-red-700' : '';
    const titleClasses = status?.lastError ? 'text-red-600' : 'text-indigo-500';
    const retryTimestamp = status ? `${utils.dateFormat.format(status.retryStartTimeMSecs)}` : '';
    const retryMessage = (status?.numRetries > 0) ? `${status.numRetries} retries since ${retryTimestamp}.\n` : '';

    const result = html`
      <div class="border-l-2 ${borderColor}">
        <header class="flex items-center justify-start pl-1 py-2 cursor-pointer select-none relative ${errorClasses}">
          <span class="${titleClasses}">${profile.name} - ${profile.sinkType}(${profile.version})</span>
          <span class="${timesClasses} ml-auto mr-2" @click=${this._deleteClicked}></span>
        </header>

        <form class="relative">
          <div id="form-header">
            <pre class="${status?.lastError ? '' : 'hidden'}"><textarea
              class="my-2 w-full border-2 border-red-500 focus:outline-none focus:border-red-700"
            >${retryMessage}${status?.lastError}</textarea></pre>

            <div id="common-fields" @change=${this._fieldChange}>
              <label for="batchSize">Batch Size</label>
              <input type="number" id="batchSize" name="batchSize" .value=${profile.batchSize} min="1" />
              <label for="maxWriteDelayMSecs">Max Write Delay (msecs)</label>
              <input type="number" id="maxWriteDelayMSecs" name="maxWriteDelayMSecs" .value=${profile.maxWriteDelayMSecs} min="0" />
              <label for="persistentChannel">Persistent Buffer</label>
              <input type="checkbox" id="persistentChannel" name="persistentChannel"
                class="kds-checkbox mr-auto" .checked=${profile.persistentChannel} />
            </div>
          </div>
          <div id="form-content">
            ${this.sinkTypeTemplateHolder.tag}
          </div>
        </form>
      </div>
    `;
    return result;
  }
}

window.customElements.define('event-sink-config', EventSinkConfig);
