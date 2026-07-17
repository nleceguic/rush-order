// Shared handleSummary() so every scenario produces the same HTML/JSON
// report shape for CI to upload as an artifact, instead of duplicating
// this in all four scenario files.
//
// htmlReport is pulled from k6-reporter at k6's init-context import time
// (k6 fetches remote ES module imports before the run starts, same as any
// other k6 script dependency). It's pinned to a tag, not `main`, so a
// report-library update can't silently change CI output — bump the tag
// deliberately if you want a newer version.
import { htmlReport } from 'https://raw.githubusercontent.com/benc-uk/k6-reporter/v2.4.0/dist/bundle.js';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';

export function buildSummary(data, scenarioName) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    [`reports/${scenarioName}.html`]: htmlReport(data),
    [`reports/${scenarioName}.json`]: JSON.stringify(data),
  };
}
