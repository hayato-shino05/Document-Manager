import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

type BreakdownTableProps = {
  title: string;
  description: string;
  rows: Array<{ key: string; count: number }>;
};

export function BreakdownTable({ title, description, rows }: BreakdownTableProps) {
  return (
    <Card>
      <CardHeader><CardTitle>{title}</CardTitle><CardDescription>{description}</CardDescription></CardHeader>
      <CardContent>
        {rows.length === 0 ? (
          <p className="py-6 text-sm text-stone-500">No events reported for this dimension in the selected range.</p>
        ) : (
          <Table>
            <TableHeader><TableRow><TableHead scope="col">Dimension</TableHead><TableHead scope="col" className="text-right">Events</TableHead></TableRow></TableHeader>
            <TableBody>{rows.map((row) => <TableRow key={row.key}><TableCell className="font-medium">{row.key}</TableCell><TableCell className="text-right tabular-nums">{row.count.toLocaleString()}</TableCell></TableRow>)}</TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
