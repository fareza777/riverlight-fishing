import React from "react";
import { AbsoluteFill, useVideoConfig } from "remotion";
import { C, Picture, RiverMark, sans, Wordmark } from "./theme";

export const AppIcon: React.FC = () => (
  <AbsoluteFill>
    <Picture src="identity/golden-carp-master.png" />
  </AbsoluteFill>
);

export const FeatureGraphic: React.FC = () => {
  const { width } = useVideoConfig();
  const s = width / 1024;
  return (
    <AbsoluteFill style={{ background: C.ink }}>
      <Picture src="identity/riverlight-keyart.png" />
      <AbsoluteFill
        style={{
          background:
            "linear-gradient(90deg, rgba(4,35,41,.50) 0%, rgba(4,35,41,.16) 48%, transparent 72%)",
        }}
      />
      <div
        style={{
          position: "absolute",
          left: 62 * s,
          top: 102 * s,
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          width: 417 * s,
        }}
      >
        <RiverMark size={64 * s} />
        <div style={{ marginTop: 17 * s }}>
          <Wordmark size={70 * s} compact />
        </div>
        <div
          style={{
            width: 78 * s,
            height: 1 * s,
            background: C.gold,
            marginTop: 25 * s,
            marginBottom: 19 * s,
          }}
        />
        <div
          style={{
            fontFamily: sans,
            fontSize: 17 * s,
            color: C.paper,
            fontWeight: 500,
            letterSpacing: 0.3 * s,
          }}
        >
          Every cast holds a story.
        </div>
      </div>
    </AbsoluteFill>
  );
};

export const TrailerPoster: React.FC = () => (
  <AbsoluteFill style={{ background: C.ink }}>
    <Picture src="identity/riverlight-keyart.png" />
    <AbsoluteFill
      style={{
        background: "linear-gradient(90deg, #06252b99, transparent 80%)",
      }}
    />
    <div style={{ position: "absolute", left: 130, top: 265 }}>
      <Wordmark size={150} />
    </div>
    <div
      style={{
        position: "absolute",
        left: 174,
        top: 680,
        fontFamily: sans,
        fontSize: 33,
        color: C.paper,
        letterSpacing: 1,
      }}
    >
      One old journal. Five living legends.
    </div>
  </AbsoluteFill>
);
