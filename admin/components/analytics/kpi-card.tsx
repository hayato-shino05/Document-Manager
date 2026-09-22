import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

type KpiCardProps = {
  label: string;
  value: number | null;
  description: string;
};

export function KpiCard({ label, value, description }: KpiCardProps) {
  return (
    <Card>
      <CardHeader>
        <CardDescription>{label}</CardDescription>
        <CardTitle className="text-3xl tabular-nums">{value === null ? "Not reported" : value.toLocaleString()}</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="text-sm text-stone-600">{description}</p>
      </CardContent>
    </Card>
  );
}
