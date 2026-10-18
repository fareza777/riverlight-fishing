import React from "react";
import {
  AbsoluteFill,
  interpolate,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from "remotion";
import { Video } from "@remotion/media";
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
  WaterLines,
  Wordmark,
} from "../theme";

type Kind = "cast" | "reel" | "collection" | "night" | "cabin";
const copy: Record<
  Kind,
  { shot: string; clip?: string; eyebrow: string; lines: string[]; sub: string }
> = {
  cast: {
    shot: "river-boat",
    clip: "river-row",
    eyebrow: "Take a breath. Take the boat.",
    lines: ["Cast away", "the everyday."],
    sub: "Explore at your own pace.",
  },
  reel: {
    shot: "river-reel",
    clip: "river-reel",
    eyebrow: "Wait for it…",
    lines: ["Now,", "reel."],
    sub: "Find the rhythm. Keep the line.",
  },
  collection: {
    shot: "river-catch",
    eyebrow: "Every catch is a discovery.",
    lines: ["48 species.", "One more cast."],
    sub: "Build a journal full of firsts.",
  },
  night: {
    shot: "lake-night",
    clip: "lake-night",
    eyebrow: "The water changes after sunset.",
    lines: ["Some legends", "wake at night."],
    sub: "Follow the ripples after dark.",
  },
  cabin: {
    shot: "cabin",
    clip: "cabin",
    eyebrow: "A place to return to.",
    lines: ["Make it", "your haven."],
    sub: "Decorate Grandpa’s riverside cabin.",
  },
};

const PlayScene: React.FC<{ kind: Kind }> = ({ kind }) => {
  const f = useCurrentFrame();
  const { width, height, fps, durationInFrames } = useVideoConfig();
  const portrait = height > width;
  const d = copy[kind];
  return (
    <SceneBase>
      <Picture
        src={`gameplay/${d.shot}.png`}
        style={{
          position: "absolute",
          inset: 0,
          filter: "blur(24px) brightness(.34)",
          scale: 1.15,
        }}
      />
      <AbsoluteFill
        style={{
          background: portrait
            ? "linear-gradient(180deg,#06252b88,#06252b00 55%,#06252b55)"
            : "linear-gradient(90deg,#06252bf5 0%,#06252bbf 48%,#06252b40)",
        }}
      />
      {!portrait && <WaterLines />}
      <div
        style={{
          position: "absolute",
          left: portrait ? (kind === "collection" ? 68 : 0) : 1160,
          top: portrait ? (kind === "collection" ? 760 : 0) : 45,
          width: portrait ? (kind === "collection" ? 944 : 1080) : 554,
          height: portrait ? (kind === "collection" ? 1020 : 1920) : 985,
          overflow: "hidden",
          borderRadius: portrait ? 0 : 24,
          border: portrait ? "none" : "2px solid #ebbf6b50",
          boxShadow: portrait ? "none" : "0 20px 80px #0007",
          scale: portrait
            ? 1
            : interpolate(f, [0, 0.7 * fps], [1.04, 1], clamp),
        }}
      >
        {d.clip ? (
          <Video
            name={`${kind} actual gameplay`}
            src={staticFile(`gameplay/${d.clip}.mp4`)}
            muted
            loop
            premountFor={fps}
            objectFit="cover"
            style={
              kind === "cabin"
                ? {
                    position: "absolute",
                    width: "135%",
                    height: "135%",
                    left: "-17.5%",
                    top: -50,
                  }
                : { width: "100%", height: "100%" }
            }
          />
        ) : (
          <Picture
            src={`gameplay/${d.shot}.png`}
            width={portrait ? 944 : 554}
            height={portrait ? 1678 : 985}
            style={
              portrait
                ? {
                    position: "absolute",
                    left: 0,
                    top: 0,
                    width: 944,
                    height: 1678,
                  }
                : undefined
            }
          />
        )}
      </div>
      {portrait && (
        <AbsoluteFill
          style={{
            pointerEvents: "none",
            background:
              "linear-gradient(180deg,#06252bf8 0%,#06252bed 12%,#06252b99 28%,#06252b00 43%,#06252b00 84%,#06252bd9 100%)",
          }}
        />
      )}
      <div
        style={{
          position: "absolute",
          left: portrait ? 80 : 130,
          top: portrait ? 220 : 252,
          width: portrait ? 920 : 940,
        }}
      >
        <RevealText>
          <Eyebrow style={{ fontSize: portrait ? 24 : 25, letterSpacing: 3 }}>
            {d.eyebrow}
          </Eyebrow>
        </RevealText>
        <div
          style={{
            fontFamily: serif,
            fontSize: portrait
              ? kind === "collection"
                ? 126
                : 137
              : kind === "collection"
                ? 133
                : 150,
            lineHeight: 0.91,
            fontWeight: 500,
            letterSpacing: -3,
            marginTop: 36,
          }}
        >
          <RevealText delay={0.08}>{d.lines[0]}</RevealText>
          <RevealText delay={0.2} style={{ color: C.gold, marginTop: 9 }}>
            {d.lines[1]}
          </RevealText>
        </div>
        <RevealText
          delay={0.38}
          style={{
            fontFamily: sans,
            fontSize: portrait ? 36 : 35,
            lineHeight: 1.5,
            color: C.muted,
            marginTop: 35,
            maxWidth: portrait ? 850 : 770,
          }}
        >
          {d.sub}
        </RevealText>
        {kind === "collection" && !portrait && (
          <div style={{ display: "flex", gap: 16, marginTop: 44 }}>
            {["brook_trout", "lantern_koi", "moon_eel"].map((fish, i) => (
              <div
                key={fish}
                style={{
                  width: 210,
                  height: 155,
                  background: C.paper,
                  borderRadius: 8,
                  padding: 10,
                  rotate: `${(i - 1) * 3}deg`,
                  opacity: interpolate(
                    f,
                    [(0.5 + i * 0.12) * fps, (0.8 + i * 0.12) * fps],
                    [0, 1],
                    clamp,
                  ),
                }}
              >
                <Picture
                  src={`fish/${fish}.png`}
                  fit="contain"
                  width={210}
                  height={155}
                />
              </div>
            ))}
          </div>
        )}
      </div>
      <div
        style={{
          position: "absolute",
          left: portrait ? 80 : 130,
          bottom: portrait ? 88 : 72,
          display: "flex",
          alignItems: "center",
          gap: 30,
        }}
      >
        <Wordmark size={portrait ? 42 : 36} compact />
        <div style={{ width: 1, height: 24, background: "#ebbf6b66" }} />
        <span
          style={{
            fontSize: portrait ? 18 : 18,
            letterSpacing: 3,
            fontWeight: 600,
            color: C.muted,
          }}
        >
          ACTUAL GAMEPLAY
        </span>
      </div>
      {kind === "night" && <Dust />}
      <div
        style={{
          position: "absolute",
          bottom: 0,
          left: 0,
          height: 3,
          background: C.gold,
          width: `${interpolate(f, [0, durationInFrames - 1], [0, 100], clamp)}%`,
          opacity: 0.6,
        }}
      />
    </SceneBase>
  );
};

export const CastScene: React.FC = () => <PlayScene kind="cast" />;
export const ReelScene: React.FC = () => <PlayScene kind="reel" />;
export const CollectionScene: React.FC = () => <PlayScene kind="collection" />;
export const NightScene: React.FC = () => <PlayScene kind="night" />;
export const CabinScene: React.FC = () => <PlayScene kind="cabin" />;
