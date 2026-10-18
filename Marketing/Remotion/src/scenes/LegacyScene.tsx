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
  Eyebrow,
  Picture,
  RevealText,
  SceneBase,
  serif,
  sans,
  Wordmark,
} from "../theme";

export const LegacyScene: React.FC = () => {
  const f = useCurrentFrame();
  const { width, height, fps } = useVideoConfig();
  const portrait = height > width;
  return (
    <SceneBase>
      <Picture
        src="story/memory_1_1.png"
        style={{
          position: "absolute",
          inset: 0,
          scale: interpolate(f, [0, 4 * fps], [1.02, 1.1], clamp),
        }}
      />
      <AbsoluteFill
        style={{
          background: portrait
            ? "linear-gradient(180deg,#06252bf5,#06252b77 46%,#06252bdd)"
            : "linear-gradient(90deg,#06252bf2,#06252ba1 55%,#06252b44)",
        }}
      />
      <div
        style={{
          position: "absolute",
          left: portrait ? 80 : 130,
          top: portrait ? 250 : 240,
          width: portrait ? 920 : 1010,
        }}
      >
        <RevealText>
          <Eyebrow style={{ fontSize: 24 }}>
            A journal. A voice. A way back.
          </Eyebrow>
        </RevealText>
        <RevealText
          delay={0.16}
          style={{
            fontFamily: serif,
            fontSize: portrait ? 144 : 155,
            fontWeight: 500,
            lineHeight: 0.94,
            letterSpacing: -2,
            marginTop: 42,
          }}
        >
          “Patience is
          <br />
          the real <span style={{ color: C.gold }}>bait.</span>”
        </RevealText>
        <RevealText
          delay={0.45}
          style={{
            fontFamily: sans,
            fontSize: portrait ? 35 : 34,
            color: C.muted,
            marginTop: 38,
          }}
        >
          Uncover Grandpa’s narrated memories.
        </RevealText>
      </div>
      <div
        style={{
          position: "absolute",
          right: portrait ? 90 : 140,
          bottom: portrait ? 210 : 88,
          width: portrait ? 435 : 410,
          height: portrait ? 773 : 730,
          border: "2px solid #f0deac88",
          borderRadius: 20,
          overflow: "hidden",
          rotate: "3deg",
          boxShadow: "0 18px 70px #00191c55",
          opacity: interpolate(f, [0.3 * fps, 0.8 * fps], [0, 1], clamp),
        }}
      >
        <Picture
          src="gameplay/journal.png"
          width={portrait ? 435 : 410}
          height={portrait ? 773 : 730}
        />
      </div>
      <div
        style={{
          position: "absolute",
          bottom: portrait ? 100 : 74,
          left: portrait ? 80 : 130,
        }}
      >
        <Wordmark size={portrait ? 44 : 38} compact />
      </div>
      <Dust />
    </SceneBase>
  );
};
