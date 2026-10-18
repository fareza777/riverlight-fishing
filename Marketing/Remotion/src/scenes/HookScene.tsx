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
  Wordmark,
} from "../theme";

export const HookScene: React.FC = () => {
  const f = useCurrentFrame();
  const { width, height, fps } = useVideoConfig();
  const portrait = height > width;
  const second = f >= 1.2 * fps;
  return (
    <SceneBase>
      <Picture
        src="story/intro_2.png"
        style={{
          position: "absolute",
          inset: 0,
          scale: interpolate(f, [0, 3 * fps], [1.03, 1.12], clamp),
        }}
      />
      <AbsoluteFill
        style={{
          background: portrait
            ? "linear-gradient(180deg,#06252b6e,#06252bbf 58%,#06252bee)"
            : "linear-gradient(90deg,#06252bf2,#06252b88 58%,#06252b22)",
        }}
      />
      <div
        style={{
          position: "absolute",
          left: portrait ? 80 : 130,
          top: portrait ? 260 : 210,
          right: portrait ? 80 : 600,
        }}
      >
        <RevealText>
          <Eyebrow style={{ fontSize: portrait ? 23 : 27 }}>
            His story is yours to finish.
          </Eyebrow>
        </RevealText>
        <div
          style={{
            marginTop: portrait ? 65 : 46,
            fontFamily: serif,
            fontSize: portrait ? 144 : 164,
            fontWeight: 500,
            lineHeight: 0.92,
            letterSpacing: -3,
          }}
        >
          <RevealText delay={0.12}>
            One old
            <br />
            journal.
          </RevealText>
          <div
            style={{
              color: C.gold,
              marginTop: portrait ? 65 : 38,
              opacity: interpolate(f, [1.2 * fps, 1.5 * fps], [0, 1], clamp),
              translate: `0 ${interpolate(f, [1.2 * fps, 1.65 * fps], [28, 0], clamp)}px`,
            }}
          >
            Five living
            <br />
            legends.
          </div>
        </div>
      </div>
      <div
        style={{
          position: "absolute",
          bottom: portrait ? 120 : 80,
          left: portrait ? 80 : 130,
          opacity: second ? 1 : 0.7,
        }}
      >
        <Wordmark size={portrait ? 50 : 48} compact />
      </div>
      <Dust />
    </SceneBase>
  );
};
