// Voz sintetizada en iOS para la app LSRD (AVSpeechSynthesizer).
#import <AVFoundation/AVFoundation.h>

static AVSpeechSynthesizer *lsrdSintetizador = nil;

extern "C" void LSRD_Hablar(const char *texto, const char *idioma, float velocidad)
{
    if (texto == NULL) return;
    if (lsrdSintetizador == nil) lsrdSintetizador = [[AVSpeechSynthesizer alloc] init];
    NSString *frase = [NSString stringWithUTF8String:texto];
    NSString *codigo = idioma != NULL ? [NSString stringWithUTF8String:idioma] : @"es-MX";
    AVSpeechUtterance *u = [AVSpeechUtterance speechUtteranceWithString:frase];
    AVSpeechSynthesisVoice *voz = [AVSpeechSynthesisVoice voiceWithLanguage:codigo];
    if (voz == nil) voz = [AVSpeechSynthesisVoice voiceWithLanguage:@"es-ES"];
    u.voice = voz;
    u.rate = AVSpeechUtteranceDefaultSpeechRate * MAX(0.5f, MIN(velocidad, 2.0f));
    [lsrdSintetizador stopSpeakingAtBoundary:AVSpeechBoundaryImmediate];
    [lsrdSintetizador speakUtterance:u];
}
