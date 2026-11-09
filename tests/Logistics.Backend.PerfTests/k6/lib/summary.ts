import { ALL_OPERATIONS, API_BASE_URL, GENERATE_REPORTS, SCENARIO } from './config.ts';

export interface K6MetricValues {
  readonly count?: number;
  readonly rate?: number;
  readonly avg?: number;
  readonly med?: number;
  readonly max?: number;
  readonly 'p(90)'?: number;
  readonly 'p(95)'?: number;
  readonly 'p(99)'?: number;
}

export interface K6Metric {
  readonly values?: K6MetricValues;
  readonly thresholds?: Record<string, { readonly ok?: boolean }>;
}

export interface K6SummaryData {
  readonly metrics?: Record<string, K6Metric>;
}

function fmt(val: number | undefined): string {
  if (val === undefined || val === null || Number.isNaN(val)) {
    return '-';
  }
  return `${val.toFixed(2)} ms`;
}

function pad(str: string | number, len: number, rightAlign = false): string {
  const s = String(str);
  if (s.length >= len) return s;
  const spaces = ' '.repeat(len - s.length);
  return rightAlign ? spaces + s : s + spaces;
}

function escapeXml(unsafe: string): string {
  return String(unsafe)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

export function buildJUnitXml(data: K6SummaryData): string {
  const metrics = data.metrics || {};
  const testCases: Array<{
    classname: string;
    name: string;
    ok: boolean;
    thresholdExpr: string;
  }> = [];
  let failures = 0;

  for (const [metricName, metric] of Object.entries(metrics)) {
    if (!metric.thresholds) continue;
    for (const [thresholdExpr, result] of Object.entries(metric.thresholds)) {
      const ok = result && result.ok !== false;
      if (!ok) failures++;
      testCases.push({
        classname: metricName,
        name: `${metricName} [${thresholdExpr}]`,
        ok: Boolean(ok),
        thresholdExpr,
      });
    }
  }

  const lines = [
    '<?xml version="1.0" encoding="UTF-8"?>',
    `<testsuites name="k6-${escapeXml(SCENARIO)}" tests="${testCases.length}" failures="${failures}">`,
    `  <testsuite name="Logistics.Backend.PerfTests (${escapeXml(SCENARIO)})" tests="${testCases.length}" failures="${failures}">`,
  ];

  for (const tc of testCases) {
    if (tc.ok) {
      lines.push(
        `    <testcase classname="${escapeXml(tc.classname)}" name="${escapeXml(tc.name)}" />`,
      );
    } else {
      lines.push(
        `    <testcase classname="${escapeXml(tc.classname)}" name="${escapeXml(tc.name)}">`,
      );
      lines.push(`      <failure message="Threshold breached: ${escapeXml(tc.thresholdExpr)}" />`);
      lines.push('    </testcase>');
    }
  }

  lines.push('  </testsuite>');
  lines.push('</testsuites>');
  return lines.join('\n') + '\n';
}

export function formatResourceAndApiSummary(data: K6SummaryData): string {
  const m = data.metrics || {};
  const totalReqs = m.http_reqs?.values?.count || 0;
  const rps = m.http_reqs?.values?.rate || 0;
  const failRate = (m.http_req_failed?.values?.rate || 0) * 100;
  const checkRate = (m.checks?.values?.rate || 0) * 100;
  const overall: K6MetricValues = m.http_req_duration?.values || {};

  const out: string[] = [];
  out.push('');
  out.push(
    '====================================================================================================',
  );
  out.push(
    ` Logistics.Backend k6 Performance Summary | Scenario: ${SCENARIO.toUpperCase()} | Target: ${API_BASE_URL}`,
  );
  out.push(
    '====================================================================================================',
  );
  out.push(
    ` Total Requests: ${totalReqs} (${rps.toFixed(1)} req/s) | Failed: ${failRate.toFixed(2)}% | Checks Passed: ${checkRate.toFixed(2)}%`,
  );
  out.push(
    ` Overall Latency: avg=${fmt(overall.avg)} | med=${fmt(overall.med)} | p90=${fmt(overall['p(90)'])} | p95=${fmt(overall['p(95)'])}`,
  );
  out.push(
    '----------------------------------------------------------------------------------------------------',
  );
  out.push(
    ` ${pad('Resource', 10)} | ${pad('API Operation', 25)} | ${pad('Method', 6)} | ${pad('Avg', 10, true)} | ${pad('Med (p50)', 10, true)} | ${pad('p90', 10, true)} | ${pad('p95', 10, true)} | ${pad('Max', 10, true)}`,
  );
  out.push(
    '----------------------------------------------------------------------------------------------------',
  );

  let lastResource: string | null = null;
  for (const op of ALL_OPERATIONS) {
    const key = `http_req_duration{operation:${op.operation}}`;
    const vals = m[key]?.values;
    if (!vals || (vals.avg === 0 && vals.max === 0 && vals.med === 0)) {
      continue;
    }
    if (lastResource && lastResource !== op.resource) {
      out.push(
        '----------------------------------------------------------------------------------------------------',
      );
    }
    lastResource = op.resource;
    out.push(
      ` ${pad(op.resource, 10)} | ${pad(op.operation, 25)} | ${pad(op.method, 6)} | ${pad(fmt(vals.avg), 10, true)} | ${pad(fmt(vals.med), 10, true)} | ${pad(fmt(vals['p(90)']), 10, true)} | ${pad(fmt(vals['p(95)']), 10, true)} | ${pad(fmt(vals.max), 10, true)}`,
    );
  }

  out.push(
    '====================================================================================================',
  );
  out.push('');
  return out.join('\n');
}

export function buildSummaryOutputs(data: K6SummaryData): Record<string, string> {
  const outputs: Record<string, string> = {
    stdout: formatResourceAndApiSummary(data),
  };

  if (GENERATE_REPORTS) {
    const reportDir = __ENV.REPORTS_DIR || 'tests/Logistics.Backend.PerfTests/reports';
    outputs[`${reportDir}/summary.json`] = JSON.stringify(data, null, 2);
    outputs[`${reportDir}/junit.xml`] = buildJUnitXml(data);
  }

  return outputs;
}
