import wave
import math
import struct
import os

def generate_se(filepath):
    # Balloon pop sound: a quick burst of noise and some low frequency punch
    import random
    sample_rate = 44100
    duration = 0.15 # seconds
    num_samples = int(duration * sample_rate)
    
    with wave.open(filepath, 'w') as wav_file:
        wav_file.setnchannels(1)
        wav_file.setsampwidth(2)
        wav_file.setframerate(sample_rate)
        
        for i in range(num_samples):
            t = i / sample_rate
            # Envelope: fast attack, exponential decay
            env = math.exp(-t * 30)
            
            # noise + sine sweep
            freq = 400 * math.exp(-t * 20)
            sine = math.sin(2 * math.pi * freq * t)
            noise = random.uniform(-1.0, 1.0)
            
            # Mix them
            sample = (sine * 0.4 + noise * 0.6) * env
            
            # Convert to 16-bit integer
            value = int(sample * 32767.0)
            value = max(-32768, min(32767, value))
            wav_file.writeframes(struct.pack('h', value))

def generate_bgm(filepath):
    # Simple happy BGM loop (chord progression)
    sample_rate = 44100
    tempo = 120 # BPM
    beat_dur = 60.0 / tempo
    
    # C major, F major, G major, C major progression
    chords = [
        [261.63, 329.63, 392.00], # C
        [349.23, 440.00, 523.25], # F
        [392.00, 493.88, 587.33], # G
        [261.63, 329.63, 392.00]  # C
    ]
    
    with wave.open(filepath, 'w') as wav_file:
        wav_file.setnchannels(1)
        wav_file.setsampwidth(2)
        wav_file.setframerate(sample_rate)
        
        for chord in chords:
            # Play each chord for 2 beats (1 measure)
            duration = beat_dur * 2
            num_samples = int(duration * sample_rate)
            for i in range(num_samples):
                t = i / sample_rate
                
                # Simple envelope for each chord
                env = 1.0
                if t < 0.05: env = t / 0.05
                elif t > duration - 0.05: env = (duration - t) / 0.05
                
                # Mix the 3 notes
                sample = 0
                for freq in chord:
                    # Square wave
                    sine = math.sin(2 * math.pi * freq * t)
                    sq = 1.0 if sine > 0 else -1.0
                    sample += sq * 0.1 # low volume
                
                # Apply envelope
                sample *= env
                
                value = int(sample * 32767.0)
                value = max(-32768, min(32767, value))
                wav_file.writeframes(struct.pack('h', value))

if __name__ == '__main__':
    os.makedirs(r'd:\TapGame\TapGame\Assets\Sounds', exist_ok=True)
    generate_se(r'd:\TapGame\TapGame\Assets\Sounds\se_tap.wav')
    generate_bgm(r'd:\TapGame\TapGame\Assets\Sounds\bgm_main.wav')
    print('Generated audio files successfully.')
