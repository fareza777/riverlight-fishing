import React from "react";
import {
  AbsoluteFill,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from "remotion";
import { Video } from "@remotion/media";
import { C, Eyebrow, RevealText, SceneBase, serif, Wordmark } from "../theme";

const waters = [
  { id: "river", name: "Riverlight River" },
  { id: "lake", name: "Willow Lake" },
  { id: "gorge", name: "Pinefall Gorge" },
  { id: "estuary", name: "Saltreed Estuary" },
];
export const WorldsScene: React.FC = () => {
  const f = useCurrentFrame();
  const { width, height, fps } = useVideoConfig();
  const portrait = height > width;
  const active = Math.min(3, Math.floor(f / fps));
  return (
    <SceneBase>
      {portrait ? (
        <>
          <Video
            key={active}
            name={waters[active].name}
            src={staticFile(`gameplay/${waters[active].id}-row.mp4`)}
            muted
            loop
            premountFor={fps}
            objectFit="cover"
            style={{ width, height }}
          />
          <AbsoluteFill
            style={{
              background:
                "linear-gradient(180deg,#06252bf5 0%,#06252ba0 25%,transparent 47%,#06252b00 76%,#06252bea 100%)",
            }}
          />
        </>
      ) : (
        <>
          <AbsoluteFill
            style={{
              display: "flex",
              flexDirection: "row",
              gap: 14,
              padding: 32,
            }}
          >
            {waters.map((w, i) => (
              <div
                key={w.id}
                style={{
                  position: "relative",
                  flex: i === active ? 1.32 : 0.95,
                  overflow: "hidden",
                  borderRadius: 12,
                  border: `1px solid ${i === active ? C.gold : "#ebbf6b33"}`,
                }}
              >
                <Video
                  name={w.name}
                  src={staticFile(`gameplay/${w.id}-row.mp4`)}
                  muted
                  loop
                  premountFor={fps}
                  objectFit="cover"
                  style={{
                    width: "100%",
                    height: "100%",
                    opacity: i === active ? 1 : 0.62,
                  }}
                />
                <AbsoluteFill
                  style={{
                    background:
                      "linear-gradient(0deg,#06252bf5,transparent 50%,#06252b88)",
                  }}
                />
                <div
                  style={{
                    position: "absolute",
                    left: 28,
                    bottom: 110,
                    right: 20,
                    fontFamily: serif,
                    fontSize: 49,
                    color: i === active ? C.gold : C.paper,
                    lineHeight: 1,
                  }}
                >
                  {w.name}
                </div>
              </div>
            ))}
          </AbsoluteFill>
          <AbsoluteFill
            style={{
              background: "linear-gradient(180deg,#06252bd9,transparent 35%)",
              pointerEvents: "none",
            }}
          />
        </>
      )}
      <div
        style={{
          position: "absolute",
          top: portrait ? 225 : 110,
          left: portrait ? 80 : 130,
          right: 80,
        }}
      >
        <RevealText>
          <Eyebrow style={{ fontSize: 24 }}>Find your favourite place.</Eyebrow>
        </RevealText>
        <RevealText
          delay={0.12}
          style={{
            fontFamily: serif,
            fontWeight: 500,
            fontSize: portrait ? 132 : 128,
            lineHeight: 0.92,
            letterSpacing: -2,
            marginTop: 30,
          }}
        >
          Four waters.
          <br />
          <span style={{ color: C.gold }}>One adventure.</span>
        </RevealText>
      </div>
      {portrait && (
        <div
          style={{
            position: "absolute",
            bottom: 220,
            left: 80,
            fontFamily: serif,
            fontSize: 66,
            color: C.gold,
          }}
        >
          {waters[active].name}
        </div>
      )}
      <div
        style={{
          position: "absolute",
          bottom: portrait ? 100 : 65,
          left: portrait ? 80 : 130,
        }}
      >
        <Wordmark size={portrait ? 44 : 38} compact />
      </div>
    </SceneBase>
  );
};
