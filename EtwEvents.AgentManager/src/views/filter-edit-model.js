﻿import AgentState from '../js/agentState.js';

class FilterEditModel {
  constructor(agentState, diagnostics) {
    this._agentState = agentState || new AgentState();
    this.diagnostics = diagnostics || [];
  }

  activate(agentState) {
    this._agentState = agentState;
  }

  get header() { return this._agentState.processingOptions.filter.filterParts[0]; }
  set header(value) { this._agentState.processingOptions.filter.filterParts[0] = value; this.diagnostics = [];}

  get body() { return this._agentState.processingOptions.filter.filterParts[1]; }
  set body(value) { this._agentState.processingOptions.filter.filterParts[1] = value; this.diagnostics = [];}

  get init() { return this._agentState.processingOptions.filter.filterParts[2]; }
  set init(value) { this._agentState.processingOptions.filter.filterParts[2] = value; this.diagnostics = [];}

  get method() { return this._agentState.processingOptions.filter.filterParts[3]; }
  set method(value) { this._agentState.processingOptions.filter.filterParts[3] = value; this.diagnostics = [];}
}

export default FilterEditModel;
