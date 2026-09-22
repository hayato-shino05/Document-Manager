import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

type TrendPoint = { label: string; value: number };

type TrendChartProps = {
  title: string;
  description: string;
  points: TrendPoint[];
  valueLabel: string;
};

export function TrendChart({ title, description, points, valueLabel }: TrendChartProps) {
  const max = Math.max(...points.map((point) => point.value), 1);
  const width = 720;
  const height = 220;
  const padding = 28;
  const chartWidth = width - padding * 2;
  const chartHeight = height - padding * 2;
  const path = points.map((point, index) => {
    const x = padding + (points.length <= 1 ? chartWidth / 2 : (index / (points.length - 1)) * chartWidth);
    const y = padding + chartHeight - (point.value / max) * chartHeight;
    return `${index === 0 ? "M" : "L"} ${x.toFixed(1)} ${y.toFixed(1)}`;
  }).join(" ");

  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        {points.length === 0 ? (
          <p className="py-16 text-center text-sm text-stone-500">No data in this range.</p>
        ) : (
          <>
            <div className="overflow-x-auto">
              <svg className="h-auto min-w-[520px]" viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`${title}: ${valueLabel} over time`}>
                <title>{`${title}: ${valueLabel} over time`}</title>
                {[0, 0.5, 1].map((ratio) => {
                  const y = padding + chartHeight - ratio * chartHeight;
                  return <line key={ratio} x1={padding} x2={width - padding} y1={y} y2={y} stroke="hsl(30 12% 86%)" strokeWidth="1" />;
                })}
                <path d={path} fill="none" stroke="hsl(174 62% 28%)" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
                {points.map((point, index) => {
                  const x = padding + (points.length <= 1 ? chartWidth / 2 : (index / (points.length - 1)) * chartWidth);
                  const y = padding + chartHeight - (point.value / max) * chartHeight;
                  return <circle key={`${point.label}-${index}`} cx={x} cy={y} r="4" fill="white" stroke="hsl(174 62% 28%)" strokeWidth="2"><title>{`${point.label}: ${point.value.toLocaleString()}`}</title></circle>;
                })}
                <text x={padding} y={height - 4} fill="hsl(25 8% 42%)" fontSize="11">{points[0]?.label}</text>
                <text x={width - padding} y={height - 4} textAnchor="end" fill="hsl(25 8% 42%)" fontSize="11">{points.at(-1)?.label}</text>
              </svg>
            </div>
            <details className="mt-3 text-sm">
              <summary className="cursor-pointer font-medium text-stone-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-700/50">View data table</summary>
              <table className="mt-2 w-full text-left">
                <caption className="sr-only">{title} data</caption>
                <thead><tr><th className="py-2 pr-4 font-medium">Date</th><th className="py-2 font-medium">{valueLabel}</th></tr></thead>
                <tbody>{points.map((point) => <tr key={point.label} className="border-t border-stone-100"><td className="py-2 pr-4">{point.label}</td><td className="py-2 tabular-nums">{point.value.toLocaleString()}</td></tr>)}</tbody>
              </table>
            </details>
          </>
        )}
      </CardContent>
    </Card>
  );
}
