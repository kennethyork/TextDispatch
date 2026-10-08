interface SparklineProps {
  values: number[];
  /** Fixed upper bound; omit to auto-scale to the window's peak. */
  max?: number;
  color?: string;
  height?: number;
}

const VIEW_WIDTH = 240;

/**
 * Dependency-free sparkline. `preserveAspectRatio="none"` lets one SVG scale
 * to whatever the panel width happens to be.
 */
export function Sparkline({
  values,
  max,
  color = "#5eead4",
  height = 44,
}: SparklineProps) {
  if (values.length === 0) {
    return <div className="sparkline sparkline--empty" style={{ height }} />;
  }

  const peak = max ?? Math.max(...values, 1);
  const ceiling = peak <= 0 ? 1 : peak;
  const step = values.length > 1 ? VIEW_WIDTH / (values.length - 1) : VIEW_WIDTH;

  const points = values.map((value, index) => {
    const clamped = Math.min(Math.max(value, 0), ceiling);
    const y = height - (clamped / ceiling) * (height - 4) - 2;
    return `${(index * step).toFixed(2)},${y.toFixed(2)}`;
  });

  const line = `M ${points.join(" L ")}`;
  const area = `${line} L ${VIEW_WIDTH},${height} L 0,${height} Z`;
  const gradientId = `spark-${color.replace(/[^a-z0-9]/gi, "")}-${height}`;

  return (
    <svg
      className="sparkline"
      viewBox={`0 0 ${VIEW_WIDTH} ${height}`}
      preserveAspectRatio="none"
      style={{ height }}
      role="img"
      aria-hidden="true"
    >
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity="0.35" />
          <stop offset="100%" stopColor={color} stopOpacity="0" />
        </linearGradient>
      </defs>
      <path d={area} fill={`url(#${gradientId})`} />
      <path
        d={line}
        fill="none"
        stroke={color}
        strokeWidth="1.75"
        strokeLinejoin="round"
        strokeLinecap="round"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}
