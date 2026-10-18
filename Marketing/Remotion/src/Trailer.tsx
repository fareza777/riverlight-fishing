import React from "react";
import { Audio } from "@remotion/media";
import {
  interpolate,
  Series,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from "remotion";
import { HookScene } from "./scenes/HookScene";
import {
  CastScene,
  ReelScene,
  CollectionScene,
  NightScene,
  CabinScene,
} from "./scenes/PlayScenes";
import { WorldsScene } from "./scenes/WorldsScene";
import { LegacyScene } from "./scenes/LegacyScene";
import { EndScene } from "./scenes/EndScene";
import { clamp } from "./theme";

export const RiverlightTrailer: React.FC = () => {
  const { fps } = useVideoConfig();
  const f = useCurrentFrame();
  return (
    <>
      <Series>
        <Series.Sequence
          name="The hook — one journal, five legends"
          durationInFrames={3 * fps}
          premountFor={fps}
        >
          <HookScene />
        </Series.Sequence>
        <Series.Sequence
          name="Take the boat"
          durationInFrames={3 * fps}
          premountFor={fps}
        >
          <CastScene />
        </Series.Sequence>
        <Series.Sequence
          name="Feel the bite"
          durationInFrames={3 * fps}
          premountFor={fps}
        >
          <ReelScene />
        </Series.Sequence>
        <Series.Sequence
          name="The collection"
          durationInFrames={3 * fps}
          premountFor={fps}
        >
          <CollectionScene />
        </Series.Sequence>
        <Series.Sequence
          name="Four different waters"
          durationInFrames={4 * fps}
          premountFor={fps}
        >
          <WorldsScene />
        </Series.Sequence>
        <Series.Sequence
          name="Stay after sunset"
          durationInFrames={3 * fps}
          premountFor={fps}
        >
          <NightScene />
        </Series.Sequence>
        <Series.Sequence
          name="Grandpa’s voice"
          durationInFrames={4 * fps}
          premountFor={fps}
        >
          <LegacyScene />
        </Series.Sequence>
        <Series.Sequence
          name="Come home"
          durationInFrames={3 * fps}
          premountFor={fps}
        >
          <CabinScene />
        </Series.Sequence>
        <Series.Sequence
          name="Make the next page yours"
          durationInFrames={4 * fps}
          premountFor={fps}
        >
          <EndScene />
        </Series.Sequence>
      </Series>
      <Audio
        name="Riverlight original score"
        src={staticFile("audio/campaign-music.m4a")}
        premountFor={fps}
        volume={interpolate(
          f,
          [
            0,
            0.5 * fps,
            18.8 * fps,
            19.2 * fps,
            25.2 * fps,
            25.8 * fps,
            28.5 * fps,
            30 * fps,
          ],
          [0, 0.55, 0.55, 0.19, 0.19, 0.55, 0.55, 0],
          clamp,
        )}
      />
      <Audio
        name="Grandpa — patience is the real bait"
        src={staticFile("audio/grandpa-patience.mp3")}
        from={19.1 * fps}
        durationInFrames={6.1 * fps}
        volume={0.94}
        premountFor={fps}
      />
      <Audio
        name="Cast"
        src={staticFile("audio/cast.mp3")}
        from={3.05 * fps}
        volume={0.38}
        premountFor={fps}
      />
      <Audio
        name="Hook"
        src={staticFile("audio/hook.mp3")}
        from={6.05 * fps}
        volume={0.45}
        premountFor={fps}
      />
      <Audio
        name="Water"
        src={staticFile("audio/splash_small.mp3")}
        from={9 * fps}
        volume={0.3}
        premountFor={fps}
      />
      <Audio
        name="Turn the page"
        src={staticFile("audio/book_open.mp3")}
        from={19 * fps}
        volume={0.25}
        premountFor={fps}
      />
      <Audio
        name="A new chapter"
        src={staticFile("audio/chapter.mp3")}
        from={26 * fps}
        volume={0.25}
        premountFor={fps}
      />
    </>
  );
};
