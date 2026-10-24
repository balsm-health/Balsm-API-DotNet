import React, { useState } from 'react';
import * as Icons from 'lucide-react';

// ── Icon ──────────────────────────────────────────────────────────────────────

function kebabToPascal(name: string): string {
  return name.split('-').map(s => s.charAt(0).toUpperCase() + s.slice(1)).join('');
}

interface IconProps {
  name: string;
  size?: number;
  stroke?: number;
  className?: string;
  style?: React.CSSProperties;
}

export function Icon({ name, size = 16, stroke = 1.75, className = '', style }: IconProps) {
  const iconName = kebabToPascal(name);
  const LucideIcon = (Icons as unknown as Record<string, React.ComponentType<{ size?: number; strokeWidth?: number; className?: string; style?: React.CSSProperties }>>)[iconName];
  if (!LucideIcon) return <span className={`ic ${className}`} style={{ width: size, height: size, display: 'inline-flex', ...style }} />;
  return (
    <LucideIcon
      size={size}
      strokeWidth={stroke}
      className={`ic ${className}`}
      style={{ display: 'inline-flex', flexShrink: 0, ...style }}
    />
  );
}

// ── Btn ───────────────────────────────────────────────────────────────────────

interface BtnProps {
  children?: React.ReactNode;
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'violet';
  size?: 'sm' | 'lg';
  block?: boolean;
  icon?: string;
  iconRight?: string;
  onClick?: () => void;
  disabled?: boolean;
  type?: 'button' | 'submit' | 'reset';
  style?: React.CSSProperties;
  title?: string;
}

export function Btn({ children, variant = 'primary', size, block, icon, iconRight, onClick, disabled, type = 'button', style, title }: BtnProps) {
  const cls = ['btn', variant, size, block ? 'block' : '', disabled ? 'is-disabled' : ''].filter(Boolean).join(' ');
  return (
    <button type={type} className={cls} onClick={onClick} disabled={disabled} style={style} title={title}>
      {icon && <Icon name={icon} size={size === 'lg' ? 18 : 16} />}
      {children}
      {iconRight && <Icon name={iconRight} size={size === 'lg' ? 18 : 16} />}
    </button>
  );
}

// ── IconBtn ───────────────────────────────────────────────────────────────────

interface IconBtnProps {
  icon: string;
  onClick?: () => void;
  title?: string;
  active?: boolean;
  size?: number;
}

export function IconBtn({ icon, onClick, title, active, size = 36 }: IconBtnProps) {
  return (
    <button className={`icon-btn ${active ? 'active' : ''}`} onClick={onClick} title={title} style={{ width: size, height: size }}>
      <Icon name={icon} size={size > 36 ? 18 : 16} />
    </button>
  );
}

// ── Pill ──────────────────────────────────────────────────────────────────────

interface PillProps {
  tone?: 'success' | 'info' | 'aqua' | 'warn' | 'danger' | 'violet' | 'neutral';
  icon?: string;
  dot?: boolean;
  children?: React.ReactNode;
  pulse?: boolean;
}

export function Pill({ tone = 'neutral', icon, dot = true, children, pulse }: PillProps) {
  return (
    <span className={`pill ${tone} ${pulse ? 'pulse' : ''}`}>
      {dot && !icon && <span className="dot" />}
      {icon && <Icon name={icon} size={12} />}
      {children}
    </span>
  );
}

// ── Avatar ────────────────────────────────────────────────────────────────────

interface AvatarProps {
  initials: string;
  tone?: string;
  size?: string;
}

export function Avatar({ initials, tone = '', size = '' }: AvatarProps) {
  return <span className={`avatar ${tone} ${size}`.trim()}>{initials}</span>;
}

// ── Flower ───────────────────────────────────────────────────────────────────

interface FlowerProps {
  size?: number;
}

export function Flower({ size = 40 }: FlowerProps) {
  return (
    <svg viewBox="0 0 700 700" width={size} height={size} xmlns="http://www.w3.org/2000/svg" style={{ flexShrink: 0, display: 'block' }}>
      <path fill="#01c4a2" d="m451.80518,159.04954c0.71667,30.88309-8.20398,58.20377-26.11499,82.69324-25.25928,34.53674-50.46729,69.11093-75.94373,104.00668-0.65643-0.78058-1.61401-1.78595-2.42233-2.89972-23.96298-33.01697-47.85528-66.08542-71.87952-99.05771-18.73278-25.70999-29.34878-54.03579-27.66491-86.1911 1.49042-28.46109 11.36957-54.13971 26.52933-78.08249 19.14752-30.2409 44.0809-54.98806 71.9096-77.07618 1.15024-0.91298 2.50427-1.56925 3.92102-2.44226 23.98938,18.2172 45.79834,38.4172 63.76557,62.50413 13.31128,17.84515 24.90753,36.79788 30.91406,58.37656 3.41138,12.25546 4.71124,25.09869 6.9859,38.16885z"/>
      <path fill="#1283ff" d="m580.16035,400.31783c-29.1501,10.22499-57.89025,10.1835-86.71592,0.71678-40.65194-13.35056-81.32365-26.64079-122.38414-40.08695 0.53953-0.86551 1.19979-2.0869 2.00926-3.19983 23.99604-32.99295 48.06287-65.93459 71.99749-98.972 18.6629-25.76076 42.32182-44.61033 73.42368-52.94541 27.52867-7.37749 55.00331-5.91699 82.45887,1.10208 34.67772,8.86542 65.9185,24.93118 95.52509,44.57224 1.22374,0.81182 2.26631,1.89678 3.53439,2.97441-9.91246,28.44468-22.38446,55.42838-39.74031,79.9595-12.85834,18.17423-27.30002,35.05964-45.96644,47.44037-10.60146,7.03156-22.41442,12.23658-34.14197,18.43881z"/>
      <path fill="#55d77f" d="m390.3645,596.94685c-18.73242-24.5637-27.57416-51.91002-27.4784-82.25024 0.135-42.78785 0.20651-85.57585 0.30618-128.78178 0.98988,0.24566 2.35552,0.49617 3.66412,0.92211 38.79334,12.6262 77.55977,25.3356 116.37641,37.88965 30.26709,9.78897 55.50511,26.46509 73.04324,53.46904 15.52324,23.90155 22.62435,50.48281 24.43306,78.76361 2.28448,35.72003-3.34103,70.39638-12.87185,104.62334-0.39392,1.41471-1.10361,2.74152-1.73664,4.28055-30.11562-0.63742-59.6327-4.16058-88.32644-13.08644-21.25817-6.61286-41.77987-15.12983-59.32288-29.0568-9.96344-7.90972-18.56411-17.53607-28.0868-26.77304z"/>
      <path fill="#724dd0" d="m144.70905,477.20198c17.57282-25.4062 40.84847-42.26566 69.73333-51.55023 40.73538-13.09378 81.45128-26.24799 122.57337-39.50457 0.0722,1.01734 0.256,2.39355 0.25529,3.76973-0.0204,40.79638-0.1283,81.59286-0.0729,122.38909 0.0432,31.81068-8.01778,60.96666-28.28047,85.9911-17.93479,22.14946-41.0207,37.11708-67.35842,47.57651-33.26582,13.21077-67.98337,18.57617-103.48033,20.08854-1.4672,0.0625-2.94838-0.20243-4.6077-0.32889-8.70001-28.83863-14.47057-57.99976-14.84842-88.04737-0.27994-22.2612 1.47864-44.41039 9.30288-65.39846 4.44372-11.92003 10.94117-23.07446 16.78338-34.98545z"/>
      <path fill="#02bbb5" d="m182.68147,206.56656c29.59303,8.8618 52.81991,25.7884 70.57597,50.39045 25.04085,34.69544 50.13315,69.35369 75.44833,104.36661-0.94522,0.38309-2.19729,0.98312-3.50634,1.40771-38.80597,12.58734-77.63906,25.09155-116.42147,37.75097-30.2404,9.87111-60.46036,11.21437-90.52154-0.32363C91.648883,389.94624 70.279893,372.61548 52.193583,350.79897 29.349683,323.24364 13.518573,291.88329 1.111063,258.59102 0.598213,257.21495 0.392483,255.7244 2.9999999e-6,254.10721 24.738713,236.92138 50.689393,222.42197 79.149603,212.77739c21.085157-7.14532 42.693717-12.3173 65.072377-11.36167 12.70981,0.54273 25.32613,3.27527 38.45949,5.15084z"/>
    </svg>
  );
}

// ── Brand ─────────────────────────────────────────────────────────────────────

interface BrandProps {
  size?: number;
  ar?: boolean;
}

export function Brand({ size = 38 }: BrandProps) {
  return (
    <div className="brand">
      <Flower size={size} />
      <div className="stack">
        <span className="name">Balsm<span className="tld">.health</span></span>
        <span className="ar">بلسم</span>
      </div>
    </div>
  );
}

// ── Field ─────────────────────────────────────────────────────────────────────

interface FieldProps {
  label?: string;
  hint?: React.ReactNode;
  error?: string | null;
  children: React.ReactNode;
  mono?: boolean;
  suffix?: React.ReactNode;
}

export function Field({ label, hint, error, children, mono, suffix }: FieldProps) {
  return (
    <label className={`field ${error ? 'has-error' : ''}`}>
      {label && <span className="field-label">{label}</span>}
      <span className={`field-control ${mono ? 'mono' : ''}`}>
        {children}
        {suffix && <span className="field-suffix">{suffix}</span>}
      </span>
      {error ? <span className="field-msg error">{error}</span> : hint ? <span className="field-msg">{hint}</span> : null}
    </label>
  );
}

// ── TextInput ─────────────────────────────────────────────────────────────────

interface TextInputProps {
  value: string;
  onChange?: (v: string) => void;
  placeholder?: string;
  type?: string;
  mono?: boolean;
  autoFocus?: boolean;
  onKeyDown?: React.KeyboardEventHandler<HTMLInputElement>;
  disabled?: boolean;
}

export function TextInput({ value, onChange, placeholder, type = 'text', mono, autoFocus, onKeyDown, disabled }: TextInputProps) {
  return (
    <input
      className={`text-input ${mono ? 'mono' : ''}`}
      type={type}
      value={value}
      placeholder={placeholder}
      autoFocus={autoFocus}
      disabled={disabled}
      onKeyDown={onKeyDown}
      onChange={(e) => onChange?.(e.target.value)}
    />
  );
}

// ── PasswordInput ─────────────────────────────────────────────────────────────

interface PasswordInputProps {
  value: string;
  onChange?: (v: string) => void;
  placeholder?: string;
  autoFocus?: boolean;
  onKeyDown?: React.KeyboardEventHandler<HTMLInputElement>;
  disabled?: boolean;
}

export function PasswordInput({ value, onChange, placeholder, autoFocus, onKeyDown, disabled }: PasswordInputProps) {
  const [show, setShow] = useState(false);
  return (
    <>
      <input
        className="text-input pw-input"
        type={show ? 'text' : 'password'}
        value={value}
        placeholder={placeholder}
        autoFocus={autoFocus}
        disabled={disabled}
        onKeyDown={onKeyDown}
        onChange={(e) => onChange?.(e.target.value)}
      />
      <button
        type="button"
        className="field-eye"
        tabIndex={-1}
        aria-label={show ? 'Hide password' : 'Show password'}
        onMouseDown={(e) => e.preventDefault()}
        onClick={() => setShow(s => !s)}
      >
        <Icon name={show ? 'eye-off' : 'eye'} size={15} stroke={1.75} />
      </button>
    </>
  );
}

// ── Switch ────────────────────────────────────────────────────────────────────

interface SwitchProps {
  on: boolean;
  onChange: (v: boolean) => void;
  disabled?: boolean;
}

export function Switch({ on, onChange, disabled }: SwitchProps) {
  return (
    <button type="button" className={`switch ${on ? 'on' : ''} ${disabled ? 'is-disabled' : ''}`} onClick={() => !disabled && onChange(!on)} aria-pressed={on}>
      <span className="knob" />
    </button>
  );
}

// ── Segmented ─────────────────────────────────────────────────────────────────

type SegmentedOption = string | { value: string; label: string };

interface SegmentedProps {
  options: SegmentedOption[];
  value: string;
  onChange: (v: string) => void;
  size?: 'sm';
}

export function Segmented({ options, value, onChange, size }: SegmentedProps) {
  return (
    <div className={`segmented ${size || ''}`}>
      {options.map((o) => {
        const v = typeof o === 'object' ? o.value : o;
        const l = typeof o === 'object' ? o.label : o;
        return (
          <button key={v} type="button" className={value === v ? 'active' : ''} onClick={() => onChange(v)}>{l}</button>
        );
      })}
    </div>
  );
}

// ── CopyField ─────────────────────────────────────────────────────────────────

function fallbackCopy(text: string, onDone: () => void) {
  const ta = document.createElement('textarea');
  ta.value = text;
  ta.style.cssText = 'position:fixed;top:-9999px;left:-9999px;opacity:0';
  document.body.appendChild(ta);
  ta.focus();
  ta.select();
  try { document.execCommand('copy'); onDone(); } catch { /* nothing */ }
  document.body.removeChild(ta);
}

interface CopyFieldProps {
  value: string;
  mono?: boolean;
}

export function CopyField({ value, mono = true }: CopyFieldProps) {
  const [copied, setCopied] = useState(false);
  const doCopy = () => {
    const done = () => { setCopied(true); setTimeout(() => setCopied(false), 1400); };
    if (navigator.clipboard) {
      navigator.clipboard.writeText(value).then(done).catch(() => fallbackCopy(value, done));
    } else {
      fallbackCopy(value, done);
    }
  };
  return (
    <button type="button" className={`copy-field ${mono ? 'mono' : ''}`} onClick={doCopy} title="Copy">
      <span className="cf-val">{value}</span>
      <Icon name={copied ? 'check' : 'copy'} size={14} />
    </button>
  );
}

// ── Card ──────────────────────────────────────────────────────────────────────

interface CardProps {
  title?: string;
  sub?: string;
  actions?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
  icon?: string;
  accent?: string;
  flush?: boolean;
}

export function Card({ title, sub, actions, children, className = '', icon, accent, flush }: CardProps) {
  return (
    <section className={`card ${className}`}>
      {(title || actions) && (
        <header className="card-head">
          {accent && <span className="card-accent" style={{ background: accent }} />}
          {icon && <span className="card-head-ic"><Icon name={icon} size={18} /></span>}
          {(title || sub) && (
            <div className="card-head-text">
              {title && <h3>{title}</h3>}
              {sub && <span className="card-sub">{sub}</span>}
            </div>
          )}
          {actions && <div className="card-head-actions">{actions}</div>}
        </header>
      )}
      <div className={`card-body ${flush ? 'flush' : ''}`}>{children}</div>
    </section>
  );
}
