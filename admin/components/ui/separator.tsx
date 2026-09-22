import * as React from "react";
import { cn } from "@/lib/utils";

export function Separator({ className, orientation = "horizontal", ...props }: React.HTMLAttributes<HTMLDivElement> & { orientation?: "horizontal" | "vertical" }) {
  return <div role="separator" aria-orientation={orientation} className={cn("shrink-0 bg-stone-200", orientation === "vertical" ? "h-full w-px" : "h-px w-full", className)} {...props} />;
}
