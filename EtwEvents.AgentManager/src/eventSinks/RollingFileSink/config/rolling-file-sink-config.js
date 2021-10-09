﻿import { LitMvvmElement, html, css } from '../../../lib/@kdsoft/lit-mvvm.js';
import { Queue, priorities } from '../../../lib/@nx-js/queue-util/dist/es.es6.js';
import sharedStyles from '../../../styles/kdsoft-shared-styles.js';
import styleLinks from '../../../styles/kdsoft-style-links.js';
import * as utils from '../../../js/utils.js';

class RollingFileSinkConfig extends LitMvvmElement {
  constructor() {
    super();
    this.scheduler = new Queue(priorities.HIGH);
  }

  isValid() {
    return this.renderRoot.querySelector('form').reportValidity();
  }

  _optionsChange(e) {
    e.stopPropagation();
    console.log(`${e.target.name}=${e.target.value}`);
    this.model.options[e.target.name] = utils.getFieldValue(e.target);
  }

  // first event when model is available
  beforeFirstRender() {
    //
  }


  static get styles() {
    return [
      css`
        form {
          position: relative;
          height: 100%;
          display: flex;
          flex-direction: column;
          align-items: center;
        }

        .center {
          display: flex;
          justify-content: center;
          align-items: center;
        }

        label {
          font-weight: bolder;
          color: #718096;
        }

        section {
          min-width: 75%;
        }

        section fieldset {
          padding: 5px;
          border-width: 1px;
        }

        section fieldset > div {
          display:grid;
          grid-template-columns: auto auto;
          align-items: baseline;
          row-gap: 5px;
          column-gap: 10px;
        }

        input:invalid {
          border: 2px solid red;
        }
        `,
    ];
  }

  render() {
    const opts = this.model.options;
    //const creds = this.model.credentials;

    const result = html`
      ${sharedStyles}
      <link rel="stylesheet" type="text/css" href=${styleLinks.checkbox} />
      <style>
        :host {
          display: block;
        }
      </style>
      <form>
        <h3>Rolling File Sink "${this.model.name}"</h3>
        <section id="options" class="center mb-5" @change=${this._optionsChange}>
          <fieldset>
            <legend>Options</legend>
            <div>
              <label for="directory">Directory</label>
              <input type="text" id="directory" name="directory" value=${opts.directory} required></input>
              <label for="fileNameFormat">Filename Template</label>
              <input type="text" id="fileNameFormat" name="fileNameFormat" value=${opts.fileNameFormat}></input>
              <label for="fileExtension">File Extension</label>
              <input type="text" id="fileExtension" name="fileExtension" value=${opts.fileExtension}></input>
              <label for="useLocalTime">Use Local Time</label>
              <input type="checkbox" id="useLocalTime" name="useLocalTime" .checked=${opts.useLocalTime}></input>
              <label for="fileSizeLimitKB">File-size Limit (KB)</label>
              <input type="number" id="fileSizeLimitKB" name="fileSizeLimitKB" value=${opts.fileSizeLimitKB}></input>
              <label for="maxFileCount">Max File Count</label>
              <input type="number" id="maxFileCount" name="maxFileCount" value=${opts.maxFileCount}></input>
              <label for="newFileOnStartup">New File on Startup</label>
              <input type="checkbox" id="newFileOnStartup" name="newFileOnStartup" .checked=${opts.newFileOnStartup}></input>
            </div>
          </fieldset>
        </section>
      </form>
    `;
    return result;
  }
}

window.customElements.define('rolling-file-sink-config', RollingFileSinkConfig);

const tag = (model) => html`<rolling-file-sink-config .model=${model}></rolling-file-sink-config>`;

export default tag;