import React from "react";
import {
  AbsoluteFill,
  CanvasImage,
  Easing,
  interpolate,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from "remotion";
import { loadFont } from "@remotion/fonts";

export const C = {
  ink: "#082f34",
  deep: "#06252b",
  teal: "#16545a",
  paper: "#fbf4df",
  gold: "#ebbf6b",
  muted: "#bdd1cb",
};
export const serif = "Cormorant Garamond";
export const sans = "Manrope";
Promise.all([
  loadFont({
    family: serif,
    url: staticFile("fonts/CormorantGaramond.ttf"),
    weight: "300 700",
  }),
  loadFont({
    family: sans,
    url: staticFile("fonts/Manrope.ttf"),
    weight: "200 800",
  }),
]);
export const clamp = {
  extrapolateLeft: "clamp",
  extrapolateRight: "clamp",
} as const;
export const ease = Easing.bezier(0.16, 1, 0.3, 1);

export const Picture: React.FC<{
  src: string;
  style?: React.CSSProperties;
  fit?: "cover" | "contain" | "fill";
  width?: number;
  height?: number;
}> = ({ src, style, fit = "cover", width, height }) => {
  const config = useVideoConfig();
  const drawingWidth =
    width ?? (typeof style?.width === "number" ? style.width : config.width);
  const drawingHeight =
    height ??
    (typeof style?.height === "number" ? style.height : config.height);
  return (
    <CanvasImage
      name={src}
      src={staticFile(src)}
      fit={fit}
      width={Math.round(drawingWidth)}
      height={Math.round(drawingHeight)}
      premountFor={config.fps}
      style={{ width: "100%", height: "100%", ...style }}
    />
  );
};

export const RiverMark: React.FC<{ size?: number; color?: string }> = ({
  size = 52,
  color = C.gold,
}) => (
  <svg
    width={size}
    height={size}
    viewBox="0 0 80 80"
    fill="none"
    aria-hidden="true"
  >
    <circle cx="40" cy="21" r="9" fill={color} />
    <path
      d="M16 40 Q40 22 64 40 M12 50 Q40 32 68 50 M18 61 Q40 44 62 61"
      stroke={color}
      strokeWidth="2.3"
      strokeLinecap="round"
    />
  </svg>
);

export const Wordmark: React.FC<{
  size?: number;
  color?: string;
  compact?: boolean;
}> = ({ size = 90, color = C.paper, compact = false }) => (
  <div
    style={{
      color,
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      width: "max-content",
    }}
  >
    {!compact && <RiverMark size={size * 0.72} />}
    <div
      style={{
        fontFamily: serif,
        fontWeight: 500,
        fontSize: size,
        lineHeight: 0.95,
        letterSpacing: size * 0.045,
      }}
    >
      RIVERLIGHT
    </div>
    {!compact && (
      <div
        style={{
          fontFamily: sans,
          fontSize: size * 0.137,
          fontWeight: 600,
          letterSpacing: size * 0.03,
          marginTop: size * 0.22,
        }}
      >
        A COZY FISHING ADVENTURE
      </div>
    )}
  </div>
);

export const WaterLines: React.FC<{ light?: boolean }> = ({
  light = false,
}) => (
  <AbsoluteFill
    style={{
      pointerEvents: "none",
      opacity: light ? 0.18 : 0.08,
      overflow: "hidden",
    }}
  >
    <svg
      width="100%"
      height="100%"
      viewBox="0 0 1080 1920"
      preserveAspectRatio="none"
      fill="none"
    >
      {[0, 1, 2, 3, 4, 5, 6, 7].map((i) => (
        <path
          key={i}
          d={`M -100 ${900 + i * 90} C 100 ${500 + i * 80}, 780 ${1550 + i * 40}, 1200 ${840 + i * 90}`}
          stroke={C.gold}
          strokeWidth="1.4"
        />
      ))}
    </svg>
  </AbsoluteFill>
);

export const Dust: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill style={{ pointerEvents: "none", overflow: "hidden" }}>
      {Array.from({ length: 16 }, (_, i) => (
        <div
          key={i}
          style={{
            position: "absolute",
            left: `${(i * 37 + 13) % 100}%`,
            top: `${(i * 23 + 17) % 100}%`,
            width: i % 3 === 0 ? 5 : 3,
            height: i % 3 === 0 ? 5 : 3,
            borderRadius: "50%",
            background: C.gold,
            opacity: 0.15 + 0.2 * Math.sin(frame / 36 + i),
            translate: `${Math.sin(frame / 65 + i) * 14}px ${-frame * 0.16}px`,
            boxShadow: "0 0 14px #ebbf6b90",
          }}
        />
      ))}
    </AbsoluteFill>
  );
};

export const RevealText: React.FC<{
  children: React.ReactNode;
  delay?: number;
  style?: React.CSSProperties;
}> = ({ children, delay = 0, style }) => {
  const f = useCurrentFrame();
  const { fps } = useVideoConfig();
  return (
    <div
      style={{
        opacity: interpolate(
          f,
          [delay * fps, (delay + 0.48) * fps],
          [0, 1],
          clamp,
        ),
        translate: `0 ${interpolate(f, [delay * fps, (delay + 0.65) * fps], [34, 0], { ...clamp, easing: ease })}px`,
        ...style,
      }}
    >
      {children}
    </div>
  );
};

export const Eyebrow: React.FC<{
  children: React.ReactNode;
  style?: React.CSSProperties;
}> = ({ children, style }) => (
  <div
    style={{
      fontFamily: sans,
      fontSize: 24,
      fontWeight: 700,
      letterSpacing: 5,
      color: C.gold,
      textTransform: "uppercase",
      ...style,
    }}
  >
    {children}
  </div>
);

export const SceneBase: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => (
  <AbsoluteFill
    style={{
      background: C.deep,
      color: C.paper,
      fontFamily: sans,
      overflow: "hidden",
    }}
  >
    {children}
  </AbsoluteFill>
);
