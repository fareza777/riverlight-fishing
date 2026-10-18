import React from "react";
import { AbsoluteFill, Sequence } from "remotion";
import {
  C,
  Eyebrow,
  Picture,
  RiverMark,
  sans,
  serif,
  WaterLines,
  Wordmark,
} from "./theme";

type Props = { index: number };
const titles = [
  ["Find your", "quiet."],
  ["Feel every", "bite."],
  ["48 reasons to", "cast again."],
  ["Four waters.", "One adventure."],
  ["Chase", "the night."],
  ["Follow his", "last adventure."],
  ["Make it", "your haven."],
  ["Your next story", "is out there."],
];
const subtitles = [
  "A little boat. A whole world to unwind in.",
  "Time the strike. Find the rhythm.",
  "Discover fish. Fill your journal.",
  "River, lake, gorge and open estuary.",
  "Different hours. Different discoveries.",
  "Five legends. A lifetime of memories.",
  "Bring the riverside cabin to life.",
  "Let the river lead you somewhere new.",
];
const eyebrows = [
  "A COZY FISHING ADVENTURE",
  "CAST. HOOK. REEL.",
  "A COLLECTION WORTH EXPLORING",
  "FIND YOUR FAVOURITE PLACE",
  "STAY FOR ONE MORE CAST",
  "GRANDPA’S UNFINISHED JOURNAL",
  "YOUR OWN RIVERSIDE RETREAT",
  "YOUR RIVERLIGHT JOURNEY",
];
const shots = [
  "river-boat",
  "river-reel",
  "river-catch",
  "gorge-boat",
  "lake-night",
  "journal",
  "cabin",
  "estuary-boat",
];

const Frame: React.FC<{
  src: string;
  left?: number;
  top?: number;
  width?: number;
  height?: number;
  angle?: number;
}> = ({
  src,
  left = 117,
  top = 550,
  width = 846,
  height = 1270,
  angle = -2,
}) => (
  <div
    style={{
      position: "absolute",
      left,
      top,
      width,
      height,
      rotate: `${angle}deg`,
      borderRadius: 30,
      overflow: "hidden",
      border: "3px solid #ead5a254",
      boxShadow: "0 30px 80px #00191c70",
      background: C.deep,
    }}
  >
    <Picture
      src={`gameplay/${src}.png`}
      fit="fill"
      style={{
        position: "absolute",
        top: 0,
        left: 0,
        width,
        height: (width * 16) / 9,
      }}
    />
  </div>
);

export const StoreScreenshot: React.FC<Props> = ({ index }) => {
  const n = index - 1;
  const cream = index === 3 || index === 6 || index === 7;
  const ink = cream ? C.ink : C.paper;
  const accent = cream ? "#986a29" : C.gold;
  return (
    <AbsoluteFill
      style={{
        background: cream ? "#f4eddc" : C.ink,
        color: ink,
        overflow: "hidden",
        fontFamily: sans,
      }}
    >
      <WaterLines />
      {index === 5 && (
        <AbsoluteFill
          style={{ background: "linear-gradient(180deg,#06252b,#0b303a)" }}
        />
      )}
      {index === 6 && (
        <Picture
          src="story/intro_2.png"
          style={{ position: "absolute", inset: 0, opacity: 0.09 }}
        />
      )}
      <div
        style={{
          position: "absolute",
          left: 70,
          top: 70,
          right: 70,
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
          <RiverMark size={40} color={accent} />
          <span
            style={{
              fontFamily: serif,
              fontWeight: 600,
              fontSize: 32,
              letterSpacing: 2.6,
            }}
          >
            RIVERLIGHT
          </span>
        </div>
        <div style={{ fontSize: 20, color: accent, letterSpacing: 3 }}>
          0{index} / 08
        </div>
      </div>
      <div
        style={{
          position: "absolute",
          left: 74,
          top: 173,
          right: 64,
          zIndex: 5,
        }}
      >
        <Eyebrow
          style={{
            fontSize: 21,
            letterSpacing: 3.5,
            color: accent,
            marginBottom: 25,
          }}
        >
          {eyebrows[n]}
        </Eyebrow>
        <div
          style={{
            fontFamily: serif,
            fontSize: index === 4 || index === 8 ? 113 : 132,
            fontWeight: 500,
            lineHeight: 0.87,
            letterSpacing: -2.8,
          }}
        >
          <div>{titles[n][0]}</div>
          <div style={{ color: accent, marginTop: 7 }}>{titles[n][1]}</div>
        </div>
        <div
          style={{
            fontSize: 29,
            fontWeight: 500,
            lineHeight: 1.5,
            marginTop: 26,
            color: cream ? "#426360" : "#e0e6d9",
            letterSpacing: -0.3,
          }}
        >
          {subtitles[n]}
        </div>
      </div>
      {index === 4 ? (
        <div
          style={{
            position: "absolute",
            left: 58,
            top: 585,
            right: 58,
            bottom: 104,
            display: "grid",
            gridTemplateColumns: "1fr 1fr",
            gridTemplateRows: "1fr 1fr",
            gap: 22,
          }}
        >
          {[
            ["river-boat", "Riverlight River"],
            ["lake-boat", "Willow Lake"],
            ["gorge-boat", "Pinefall Gorge"],
            ["estuary-boat", "Saltreed Estuary"],
          ].map(([src, label]) => (
            <div
              key={src}
              style={{
                position: "relative",
                borderRadius: 22,
                overflow: "hidden",
                border: "2px solid #ebbf6b44",
              }}
            >
              <Picture src={`gameplay/${src}.png`} width={471} height={605} />
              <AbsoluteFill
                style={{
                  background: "linear-gradient(0deg,#05272bdd,transparent 38%)",
                }}
              />
              <div
                style={{
                  position: "absolute",
                  bottom: 25,
                  left: 24,
                  right: 20,
                  fontFamily: serif,
                  fontSize: 37,
                  fontWeight: 600,
                  lineHeight: 1,
                }}
              >
                {label}
              </div>
            </div>
          ))}
        </div>
      ) : (
        <>
          <Frame
            src={shots[n]}
            top={index === 6 ? 555 : 550}
            left={index === 7 ? 54 : index === 2 ? 96 : 112}
            width={index === 7 ? 968 : index === 2 ? 888 : 856}
            height={index === 7 ? 1170 : 1270}
            angle={index % 2 === 0 ? 2 : -2}
          />
          {index === 7 && (
            <div
              style={{
                position: "absolute",
                top: 1757,
                left: 80,
                right: 80,
                fontFamily: serif,
                fontSize: 45,
                textAlign: "center",
                color: "#986a29",
              }}
            >
              A warm light. A place of your own.
            </div>
          )}
          {index === 3 && (
            <div
              style={{
                position: "absolute",
                left: 30,
                top: 1360,
                width: 310,
                height: 225,
                background: "#fbf4df",
                border: "2px solid #cfbd98",
                borderRadius: 18,
                rotate: "-7deg",
                boxShadow: "0 14px 38px #06252b35",
                padding: 12,
              }}
            >
              <Picture
                src="fish/lantern_koi.png"
                fit="contain"
                width={310}
                height={225}
              />
              <div
                style={{
                  position: "absolute",
                  bottom: 14,
                  left: 18,
                  fontSize: 17,
                  fontWeight: 700,
                  color: C.ink,
                  letterSpacing: 2,
                }}
              >
                LEGENDS AWAIT
              </div>
            </div>
          )}
          {index === 6 && (
            <div
              style={{
                position: "absolute",
                right: 20,
                top: 1440,
                width: 300,
                height: 400,
                border: "9px solid #fff9ea",
                rotate: "7deg",
                boxShadow: "0 15px 38px #06252b40",
              }}
            >
              <Picture src="story/memory_1_2.png" width={300} height={400} />
            </div>
          )}
        </>
      )}
      <AbsoluteFill
        style={{
          pointerEvents: "none",
          background: cream
            ? "linear-gradient(0deg,#f4eddc 0%,#f4eddc00 9%)"
            : "linear-gradient(0deg,#082f34 0%,#082f3400 9%)",
        }}
      />
      <div
        style={{
          position: "absolute",
          left: 72,
          right: 72,
          bottom: 42,
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          color: cream ? "#476762" : "#c4d8cd",
          fontSize: 17,
          fontWeight: 600,
          letterSpacing: 2,
        }}
      >
        <span>{index === 6 ? "GAMEPLAY & STORY ART" : "ACTUAL GAMEPLAY"}</span>
        <Wordmark size={25} color={ink} compact />
      </div>
    </AbsoluteFill>
  );
};

export const ContactSheet: React.FC = () => (
  <AbsoluteFill
    style={{
      background: "#e9e4d5",
      padding: 48,
      display: "grid",
      gridTemplateColumns: "repeat(4, 1fr)",
      gap: 24,
    }}
  >
    {[1, 2, 3, 4, 5, 6, 7, 8].map((i) => (
      <div
        key={i}
        style={{
          position: "relative",
          width: 324,
          height: 576,
          overflow: "hidden",
          boxShadow: "0 8px 20px #082f3420",
        }}
      >
        <div
          style={{
            width: 1080,
            height: 1920,
            scale: 0.3,
            transformOrigin: "top left",
          }}
        >
          <Sequence width={1080} height={1920}>
            <StoreScreenshot index={i} />
          </Sequence>
        </div>
      </div>
    ))}
  </AbsoluteFill>
);
