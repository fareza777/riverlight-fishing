import React from "react";
import {
  AbsoluteFill,
  interpolate,
  useCurrentFrame,
  useVideoConfig,
} from "remotion";
import {
  C,
  clamp,
  Dust,
  Picture,
  RevealText,
  SceneBase,
  serif,
  sans,
  Wordmark,
} from "../theme";

export const EndScene: React.FC = () => {
  const f = useCurrentFrame();
  const { width, height, fps } = useVideoConfig();
  const portrait = height > width;
  return (
    <SceneBase>
      <Picture
        src="identity/riverlight-keyart.png"
        style={{
          position: "absolute",
          inset: 0,
          scale: interpolate(f, [0, 4 * fps], [1.08, 1.015], clamp),
        }}
      />
      <AbsoluteFill
        style={{
          background: portrait
            ? "linear-gradient(180deg,#06252b99,#06252bbf 60%,#06252bf5)"
            : "linear-gradient(90deg,#06252be8,#06252b7d 56%,#06252b00)",
        }}
      />
      <div
        style={{
          position: "absolute",
          left: portrait ? 80 : 130,
          top: portrait ? 265 : 160,
          width: portrait ? 920 : 930,
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
        }}
      >
        <RevealText>
          <Wordmark size={portrait ? 133 : 147} />
        </RevealText>
        <RevealText
          delay={0.26}
          style={{
            fontFamily: serif,
            fontSize: portrait ? 89 : 94,
            lineHeight: 1,
            color: C.paper,
            textAlign: "center",
            marginTop: portrait ? 95 : 58,
            letterSpacing: -1,
          }}
        >
          Make the next
          <br />
          page <span style={{ color: C.gold }}>yours.</span>
        </RevealText>
        <RevealText
          delay={0.55}
          style={{
            fontFamily: sans,
            fontSize: portrait ? 35 : 32,
            color: C.paper,
            border: "1px solid #ebbf6b88",
            padding: portrait ? "25px 48px" : "22px 44px",
            marginTop: 46,
            borderRadius: 6,
            background: "#06252b44",
            letterSpacing: 0.5,
          }}
        >
          Begin your adventure
        </RevealText>
        <RevealText
          delay={0.75}
          style={{
            fontFamily: sans,
            fontSize: portrait ? 23 : 21,
            letterSpacing: 3,
            color: C.muted,
            marginTop: 25,
          }}
        >
          A COZY FISHING STORY FOR ANDROID
        </RevealText>
      </div>
      <div
        style={{
          position: "absolute",
          right: portrait ? 80 : 100,
          bottom: portrait ? 130 : 90,
          width: portrait ? 162 : 150,
          height: portrait ? 162 : 150,
          borderRadius: 30,
          overflow: "hidden",
          border: "2px solid #ebbf6b88",
          boxShadow: "0 10px 30px #00191c44",
          opacity: interpolate(f, [0.3 * fps, 0.7 * fps], [0, 1], clamp),
        }}
      >
        <Picture
          src="identity/golden-carp-master.png"
          width={portrait ? 162 : 150}
          height={portrait ? 162 : 150}
        />
      </div>
      <Dust />
      <AbsoluteFill
        style={{
          background: C.deep,
          opacity: interpolate(f, [3.8 * fps, 4 * fps], [0, 0.45], clamp),
        }}
      />
    </SceneBase>
  );
};
