import { useEffect, useState } from 'react';
import { Utensils, Search, ArrowUpRight, Leaf, Clock, MapPin, RefreshCw, Coffee, Sparkles } from 'lucide-react';
import { getMenu } from './api';
export default function App() {
  const [items, setItems] = useState([]);
  const [category, setCategory] = useState('All');
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    getMenu(controller.signal).then(setItems).catch(e => { if (e.name !== 'AbortError') setError(e.message); }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [attempt]);
  const categories = ['All', ...new Set(items.map(i => i.category))];
  const visible = items.filter(i => (category === 'All' || i.category === category) && `${i.name} ${i.description}`.toLowerCase().includes(search.toLowerCase()));
  return <div className="shell">
    <aside className="sidebar"><a className="brand" href="#"><span className="brand-icon"><Utensils size={22}/></span>Campus<span>Eats</span></a><div className="side-caption">YOUR DAILY FOOD STOP</div><nav><a className="nav-active" href="#menu"><Utensils size={18}/> Explore menu <ArrowUpRight size={16}/></a></nav><div className="side-note"><Coffee size={28}/><h3>Good food.<br/>Better study breaks.</h3><p>A little fuel for your next big idea.</p></div><div className="side-bottom"><span className="dot"/> Made for campus life<p>SE3090 · Lab 11</p></div></aside>
    <main><header><div><span className="eyebrow">THE CAMPUS EDIT</span><p>Your next favourite is here.</p></div><div className="location"><MapPin size={17}/><span>Campus canteen</span></div></header>
      <section className="hero"><div className="hero-copy"><div className="hero-tag"><Sparkles size={14}/> Fresh flavours, every day</div><h1>Your campus.<br/>Your <em>cravings.</em></h1><p>From a quick bite between lectures to a proper lunch with friends. Find your kind of delicious.</p><a href="#menu" className="hero-button">Explore the menu <ArrowUpRight size={20}/></a><div className="hero-footer"><Leaf size={16}/> Freshly prepared <span>·</span><Clock size={16}/> Worth the break</div></div><div className="hero-image" role="img" aria-label="Fresh bowl of colourful vegetables"><span className="image-label">A good day starts<br/><strong>with a good meal.</strong></span></div></section>
      <section className="menu-section" id="menu"><div className="section-heading"><div><span className="eyebrow">FIND YOUR FAVOURITE</span><h2>What’s on the menu?</h2><p>Campus classics. Student-friendly prices.</p></div><label className="search"><Search size={19}/><input aria-label="Search menu" placeholder="Search your cravings…" value={search} onChange={e=>setSearch(e.target.value)}/></label></div>
      <div className="filter-row"><div className="filters" aria-label="Food categories">{categories.map(c=><button key={c} aria-pressed={category===c} className={category===c?'selected':''} onClick={()=>setCategory(c)}>{c==='All'?'All favourites':c}</button>)}</div><span className="count">{visible.length} dishes</span></div>
      {loading ? <div className="state" role="status"><RefreshCw className="spin"/> Preparing your menu…</div> : error ? <div className="state error" role="alert"><h3>We couldn’t load the menu</h3><p>{error}</p><button onClick={()=>setAttempt(v=>v+1)}>Try again</button></div> : visible.length===0 ? <div className="state">No dishes found. Try another category or search.</div> : <div className="menu-grid">{visible.map(item=><article className="food-card" key={item.id}><div className="food-image"><img src={item.imageUrl} alt={item.name} loading="lazy" onError={e=>{e.currentTarget.onerror=null; e.currentTarget.src='/food-placeholder.svg';}}/><span className="food-category">{item.category}</span></div><div className="food-content"><div className="food-title"><h3>{item.name}</h3>{item.isVegetarian&&<span title="Vegetarian" aria-label="Vegetarian"><Leaf size={17}/></span>}</div><p>{item.description}</p><div className="food-footer"><strong><small>LKR</small> {new Intl.NumberFormat('en-LK',{maximumFractionDigits:0}).format(item.price)}</strong><span><Clock size={13}/>{item.preparationMinutes} min</span></div></div></article>)}</div>}
      </section><footer><span className="brand-small">CampusEats</span><span>Made for hungry minds.</span><span>Prices in Sri Lankan rupees</span></footer>
    </main></div>;
}
