import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { App } from './app';
import { WorkflowSignalRService } from './core/services/workflow-signalr.service';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: WorkflowSignalRService,
          useValue: {
            connectionState: () => 'connected',
            connect: async () => undefined,
            disconnect: async () => undefined
          }
        }
      ]
    }).compileComponents();
  });

  it('should create the control plane app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render Sentinel Orchestrator header', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Sentinel Orchestrator');
  });
});
